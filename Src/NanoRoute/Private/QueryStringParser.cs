/********************************************************************************
* QueryStringParser.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NanoRoute.Internals
{
    using Properties;

    internal sealed class QueryStringParser(RequestContext context, FrozenDictionary<ReadOnlyMemory<char>, ParameterParser> expectedParameters, QueryParsingConfig config) : IDisposable
    {
        #region Private
        // Extend only lists created by this parser; replace anything supplied by earlier middleware.
        private sealed class QueryValueList : List<object?>;

        private static readonly ArrayPool<char> s_arrayPool = ArrayPool<char>.Create();

        private char[]? _decodedBuffer;

        // Track only query parameters seen during this parse so required checks and duplicate detection
        // will not be confused by values that were already present in context.Parameters.
        private readonly bool[] _visited = new bool[expectedParameters.Count];

        // DelimitedSegment is a mutable struct: keep this field non-readonly so MoveNext() updates the _parameter
        // itself instead of a defensive copy.
        private DelimitedSegment _parameter = new(GetRawQuery(context.Request.RequestUri), '&');

        private int _nextDecoded;

        // Keep Parse() state-machine-free while all query value parsers complete synchronously.
        // If a parser really suspends, this helper resumes the current parameter and finishes the loop.
        private async ValueTask ParseAwaitedAsync(ParameterParser expectedParameter, ValueTask<ValueParseResult> parseResult)
        {
            AcceptParameter(expectedParameter.Definition, await parseResult.ConfigureAwait(false));
            await Parse().ConfigureAwait(false);
        }

        private void AcceptParameter(ParameterDefinition parameterDefinition, ValueParseResult parseResult)
        {
            if (!parseResult.Success)
                ThrowBadRequest(Resources.ERR_QUERY_INVALID_PARAMETER, parameterDefinition.ParameterName!);

            if (parameterDefinition.ValueParser.IsList)
            {
                if (!context.Parameters.TryGetValue(parameterDefinition.ParameterName!, out object? val) || val is not QueryValueList lst)
                    context.Parameters[parameterDefinition.ParameterName!] = lst = [];

                lst.Add(parseResult.Parsed);
            }
            else
                context.Parameters[parameterDefinition.ParameterName!] = parseResult.Parsed;
        }

        private ReadOnlyMemory<char> DecodeParameter(ReadOnlyMemory<char> source)
        {
            if (source.Span.IndexOfAny('%', '+') < 0)
                return source;

            char[] decodedBuffer = _decodedBuffer ??= s_arrayPool.Rent(_parameter.Remaining.Length);

            if (!UrlUtils.TryDecodeUrl(source.Span, decodedBuffer.AsSpan(_nextDecoded), UrlDecodeMode.Form, out int charsWritten))
                ThrowBadRequest(Resources.ERR_DECODING_FAILED);

            ReadOnlyMemory<char> result = decodedBuffer.AsMemory(_nextDecoded, charsWritten);
            _nextDecoded += charsWritten;

            return result;
        }

        private static ReadOnlyMemory<char> GetRawQuery(Uri uri)
        {
            ReadOnlyMemory<char> raw = uri.OriginalString.AsMemory();

            int fragmentIndex = raw.Span.IndexOf('#');
            if (fragmentIndex >= 0)
                raw = raw.Slice(0, fragmentIndex);

            int queryIndex = raw.Span.IndexOf('?');
            return queryIndex >= 0
                ? raw.Slice(queryIndex + 1)
                : default;
        }

        [DoesNotReturn]
        private static void ThrowBadRequest(string? error, params object[] paramz) => HttpRequestException.Throw
        (
            HttpStatusCode.BadRequest,
            Resources.ERR_BAD_REQUEST,
            !string.IsNullOrEmpty(error) ? [string.Format(Resources.Culture, error, paramz)] : []
        );
        #endregion

        public void Dispose()
        {
            if (_decodedBuffer is not null)
                s_arrayPool.Return(_decodedBuffer, clearArray: false);
        }

        public ValueTask Parse()
        {
            while (_parameter.MoveNext())
            {
                int separatorIndex = _parameter.Current.Span.IndexOf('=');
                if (separatorIndex <= 0)
                    ThrowBadRequest(null);

                ReadOnlyMemory<char> parameterName = DecodeParameter(_parameter.Current.Slice(0, separatorIndex));

                if (!expectedParameters.TryGetValue(parameterName, out ParameterParser? expectedParameter))
                {
                    switch (config.UnexpectedParameterBehavior)
                    {
                        case UnexpectedParameterBehavior.Ignore:
                            continue;

                        case UnexpectedParameterBehavior.Reject:
                            ThrowBadRequest(Resources.ERR_QUERY_UNEXPECTED_PARAMETER, parameterName);
                            break;

                        default:
                            Debug.Fail($"Unknown {nameof(UnexpectedParameterBehavior)} value: {config.UnexpectedParameterBehavior}");
                            break;
                    }
                }

                ParameterDefinition parameterDefinition = expectedParameter!.Definition;

                if (_visited[parameterDefinition.Index] && !parameterDefinition.ValueParser.IsList)
                    ThrowBadRequest(Resources.ERR_QUERY_DUPLICATE_PARAMETER, parameterDefinition.ParameterName!);

                _visited[parameterDefinition.Index] = true;

                ValueTask<ValueParseResult> parseResult = expectedParameter.Parse
                (
                    new ValueParserContext
                    {
                        Segment = DecodeParameter(_parameter.Current.Slice(separatorIndex + 1)),
                        Services = context.Services,
                        Arguments = expectedParameter.Arguments,
                        Cancellation = context.Cancellation
                    }
                );

                if (!parseResult.IsCompletedSuccessfully)
                    return ParseAwaitedAsync(expectedParameter, parseResult);

                AcceptParameter(parameterDefinition, parseResult.Result);
            }

            foreach (ParameterParser expectedParameter in expectedParameters.Values)
            {
                ParameterDefinition parameterDefinition = expectedParameter.Definition;

                if (!parameterDefinition.IsOptional && !_visited[parameterDefinition.Index])
                    ThrowBadRequest(Resources.ERR_QUERY_MISSING_PARAMETER, parameterDefinition.ParameterName!);
            }

            return default;
        }
    }
}
