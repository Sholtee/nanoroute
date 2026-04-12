/********************************************************************************
* QueryStringParser.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace NanoRoute.Internals
{
    using Properties;

    internal static class QueryStringParser
    {
        public static async ValueTask<Dictionary<string, object?>> Parse(Uri uri, IReadOnlyDictionary<string, QueryParameterDefinition> expectedParameters, IServiceProvider services, CancellationToken cancellation = default)
        {
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);

            for (DelimitedSegment parameter = new(uri.Query.AsMemory(uri.Query.AsSpan().IndexOf('?') == 0 ? 1 : 0), '&'); parameter.MoveNext();)
            {
                int separatorIndex = parameter.Current.Span.IndexOf('=');

                ReadOnlyMemory<char> rawName = separatorIndex >= 0
                    ? parameter.Current.Slice(0, separatorIndex)
                    : parameter.Current;

                if (rawName.Length is 0)
                    ThrowBadRequest(null);

                if (!expectedParameters.TryGetValue(HttpUtility.UrlDecode(rawName.ToString()), out QueryParameterDefinition? expectedParameter))
                    continue;

                if (result.ContainsKey(expectedParameter.Name))
                    ThrowBadRequest(Resources.ERR_QUERY_DUPLICATE_PARAMTER, expectedParameter.Name);

                ValueParseResult parsed = await expectedParameter.Parser.Parse
                (
                    new ValueParserContext
                    {
                        Segment = separatorIndex >= 0
                            ? parameter.Current.Slice(separatorIndex + 1)
                            : ReadOnlyMemory<char>.Empty,
                        Services = services,
                        Arguments = expectedParameter.Parser.Arguments,
                        Cancellation = cancellation
                    }
                );

                if (!parsed.Success)
                    ThrowBadRequest(Resources.ERR_QUERY_INVALID_PARAMETER, expectedParameter.Name);

                result[expectedParameter.Name] = parsed.Parsed;
            }

            if (result.Count != expectedParameters.Count)
                foreach (QueryParameterDefinition expectedParameter in expectedParameters.Values)
                    if (!expectedParameter.Optional && !result.ContainsKey(expectedParameter.Name))
                        ThrowBadRequest(Resources.ERR_QUERY_MISSING_PARAMETER, expectedParameter.Name);

            return result;
        }

        [DoesNotReturn]
        private static void ThrowBadRequest(string? error, params object[] paramz) => HttpRequestException.Throw
        (
            HttpStatusCode.BadRequest,
            Resources.ERR_BAD_REQUEST,
            !string.IsNullOrEmpty(error) ? [string.Format(Resources.Culture, error, paramz)] : []
        );
    }
}

