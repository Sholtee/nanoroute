/********************************************************************************
* RoutePatternParser.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace NanoRoute.Internals
{
    using Properties;

    internal static class RoutePatternParser
    {
        public static IEnumerable<object> ParseRoutePattern(string pattern)
        {
            bool expectSeparator = true;

            for (int offset = 0; offset < pattern.Length; offset++)
            {
                if (expectSeparator)
                {
                    if (pattern[offset] is not '/')
                        throw new InvalidOperationException(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, offset));

                    expectSeparator = false;
                    continue;
                }

                switch (pattern[offset])
                {
                    case '{':
                        ParameterDefinition parameterDefinition = ParameterDefinition.Parse(pattern, ref offset);
                        if (parameterDefinition.IsOptional)
                            throw new InvalidOperationException(Resources.ERR_OPTIONAL_PARAMETERS_NOT_SUPPORTED);

                        yield return parameterDefinition;
                        break;

                    default:
                        yield return LiteralSegmentDefinition.Parse(pattern, ref offset);
                        break;
                }

                expectSeparator = true;
            }
        }

        public static IEnumerable<ParameterDefinition> ParseQueryPattern(string pattern)
        {
            bool expectSeparator = false;
            int offset = 0;

            for (; offset < pattern.Length; offset++)
            {
                if (expectSeparator)
                {
                    if (pattern[offset] is not '&')
                        throw new InvalidOperationException(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, offset));

                    expectSeparator = false;
                    continue;
                }

                yield return ParameterDefinition.Parse(pattern, ref offset);
                expectSeparator = true;
            }

            // the pattern is either empty or ends with '&'
            if (!expectSeparator)
                throw new InvalidOperationException(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, offset));
        }
    }
}
