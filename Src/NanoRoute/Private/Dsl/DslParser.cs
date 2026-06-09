/********************************************************************************
* DslParser.cs                                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace NanoRoute.Internals
{
    using static Properties.Resources;

    internal static class DslParser
    {
        /// <summary>
        /// Valid patterns: <c>/</c>, <c>/segment/</c>, <c>/*</c>, <c>/segment/*</c>
        /// Invalid patterns: <c></c>, <c>/segment*</c>, <c>/segment/*/[another_segment]</c>
        /// </summary>
        public static IEnumerable<object> ParseRoutePattern(string pattern)
        {
            int offset = 0;

            for (int parameterIndex = 0; offset < pattern.Length && pattern[offset] is '/'; offset++)
            {
                if (++offset == pattern.Length)
                    yield break;

                switch (pattern[offset])
                {
                    case '{':
                        ParameterDefinition parameterDefinition = ParameterDefinition.Parse(pattern, ref offset);

                        if (parameterDefinition.IsOptional)
                            throw new InvalidOperationException(ERR_OPTIONAL_PARAMETERS_NOT_SUPPORTED);

                        if (parameterDefinition.ValueParser.IsList)
                            throw new InvalidOperationException(ERR_LIST_PARSERS_NOT_SUPPORTED);

                        yield return parameterDefinition with { Index = parameterIndex++ };
                        break;

                    case '*' when offset == pattern.Length - 1:
                        yield break;

                    default:
                        yield return LiteralSegmentDefinition.Parse(pattern, ref offset);
                        break;
                }
            }

            throw new ArgumentException(string.Format(Culture, ERR_INVALID_PATTERN, offset), nameof(pattern));
        }

        public static IEnumerable<ParameterDefinition> ParseQueryPattern(string pattern)
        {
            int offset = 0;

            for (int parameterIndex = 0; offset < pattern.Length; offset++)
            {
                ParameterDefinition parameterDefinition = ParameterDefinition.Parse(pattern, ref offset) with { Index = parameterIndex++ };

                if (parameterDefinition.ParameterName is null)
                    throw new InvalidOperationException(ERR_PARAMETER_NAME_REQUIRED);

                yield return parameterDefinition;

                if (++offset == pattern.Length)
                    yield break;

                if (pattern[offset] is not '&')
                    break;
            }

            throw new ArgumentException(string.Format(Culture, ERR_INVALID_PATTERN, offset), nameof(pattern));
        }
    }
}
