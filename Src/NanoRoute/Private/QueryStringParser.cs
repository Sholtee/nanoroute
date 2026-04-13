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
using System.Threading.Tasks;
using System.Web;

namespace NanoRoute.Internals
{
    using Properties;

    internal static class QueryStringParser
    {
        public static async ValueTask Parse(RequestContext context, IReadOnlyDictionary<string, QueryParameterDefinition> expectedParameters)
        {
            // Track only query parameters seen during this parse so required checks and duplicate detection
            // will not be confused by values that were already present in context.Parameters.
            bool[] visited = new bool[expectedParameters.Count];

            for (DelimitedSegment parameter = new(context.Request.RequestUri.Query.AsMemory(context.Request.RequestUri.Query.AsSpan().IndexOf('?') == 0 ? 1 : 0), '&'); parameter.MoveNext();)
            {
                int separatorIndex = parameter.Current.Span.IndexOf('=');

                ReadOnlyMemory<char> rawName = separatorIndex >= 0
                    ? parameter.Current.Slice(0, separatorIndex)
                    : parameter.Current;

                if (rawName.Length is 0)
                    ThrowBadRequest(null);

                if (!expectedParameters.TryGetValue(HttpUtility.UrlDecode(rawName.ToString()), out QueryParameterDefinition? expectedParameter))
                    continue;

                if (visited[expectedParameter.Index])
                    ThrowBadRequest(Resources.ERR_QUERY_DUPLICATE_PARAMTER, expectedParameter.Name);
                
                visited[expectedParameter.Index] = true;

                ValueParseResult parsed = await expectedParameter.Parser.Parse
                (
                    new ValueParserContext
                    {
                        Segment = separatorIndex >= 0
                            ? parameter.Current.Slice(separatorIndex + 1)
                            : ReadOnlyMemory<char>.Empty,
                        Services = context.Services,
                        Arguments = expectedParameter.Parser.Arguments,
                        Cancellation = context.Cancellation
                    }
                );

                if (!parsed.Success)
                    ThrowBadRequest(Resources.ERR_QUERY_INVALID_PARAMETER, expectedParameter.Name);

                context.Parameters[expectedParameter.Name] = parsed.Parsed;
            }

            foreach (QueryParameterDefinition expectedParameter in expectedParameters.Values)
                if (!expectedParameter.Optional && !visited[expectedParameter.Index])
                    ThrowBadRequest(Resources.ERR_QUERY_MISSING_PARAMETER, expectedParameter.Name);
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

