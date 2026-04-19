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

namespace NanoRoute.Internals
{
    using Properties;

    internal static class QueryStringParser
    {
        public static async ValueTask Parse(RequestContext context, IReadOnlyDictionary<ReadOnlyMemory<char>, ParameterParser> expectedParameters)
        {
            // Track only query parameters seen during this parse so required checks and duplicate detection
            // will not be confused by values that were already present in context.Parameters.
            bool[] visited = new bool[expectedParameters.Count];

            for (DelimitedSegment parameter = new(context.Request.RequestUri.Query.AsMemory(context.Request.RequestUri.Query.StartsWith(['?']) ? 1 : 0), '&'); parameter.MoveNext();)
            {
                int separatorIndex = parameter.Current.Span.IndexOf('=');
                if (separatorIndex <= 0)
                    ThrowBadRequest(null);

                // Uri.Query already normalizes query keys enough for descriptor matching.
                if (!expectedParameters.TryGetValue(parameter.Current.Slice(0, separatorIndex), out ParameterParser? expectedParameter))
                    continue;

                ParameterDefinition parameterDefinition = expectedParameter!.Definition;

                if (visited[parameterDefinition.Index])
                    ThrowBadRequest(Resources.ERR_QUERY_DUPLICATE_PARAMTER, parameterDefinition.ParameterName!);
                
                visited[parameterDefinition.Index] = true;

                ValueParseResult parsed = await expectedParameter.Parse
                (
                    new ValueParserContext
                    {
                        Segment = parameter.Current.Slice(separatorIndex + 1),
                        Services = context.Services,
                        Arguments = expectedParameter.Arguments,
                        Cancellation = context.Cancellation
                    }
                );
                if (!parsed.Success)
                    ThrowBadRequest(Resources.ERR_QUERY_INVALID_PARAMETER, parameterDefinition.ParameterName!);

                context.Parameters[parameterDefinition.ParameterName!] = parsed.Parsed;
            }

            // FrozenDictionary uses ImmutableArray to access Values so accessing this property is a relative fast operation
            // https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozendictionary-2.values?view=net-10.0#property-value
            foreach (ParameterParser expectedParameter in expectedParameters.Values)
            {
                ParameterDefinition parameterDefinition = expectedParameter.Definition;

                if (!parameterDefinition.IsOptional && !visited[parameterDefinition.Index])
                    ThrowBadRequest(Resources.ERR_QUERY_MISSING_PARAMETER, parameterDefinition.ParameterName!);
            }
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

