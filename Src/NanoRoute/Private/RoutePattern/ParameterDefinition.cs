/********************************************************************************
* ParameterDefinition.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Text.RegularExpressions;

namespace NanoRoute.Internals
{
    using Properties;

    internal readonly struct ParameterDefinition
    {
        private const string
            IDENTIFIER = @"[A-Za-z_]\w*",
            PARAMETER_NAME = $@"{IDENTIFIER}\??",
            PARAMETER_NAME_PATTERN = $@"\G(?:(?<parameterName>{PARAMETER_NAME}):)?";

        private static readonly Regex s_parameterName = new(PARAMETER_NAME_PATTERN);

        public static ParameterDefinition Parse(string pattern, ref int offset)
        {
            int newOffset = offset;

            if (newOffset < pattern.Length && pattern[newOffset++] == '{' && s_parameterName.Match(pattern, newOffset) is { Success: true, Index: int index, Length: int length } parsed && index == newOffset)
            {             
                string parameterName = parsed.Groups["parameterName"].Value;

                newOffset += length;

                ParameterDefinition result = new()
                {
                    ValueParser = ValueParserDefinition.Parse(pattern, ref newOffset),
                    ParameterName = parameterName.Length is 0 ? null : parameterName.TrimEnd('?'),
                    IsOptional = parameterName.EndsWith("?", StringComparison.Ordinal)
                };

                if (newOffset < pattern.Length && pattern[newOffset] == '}')
                {
                    offset = newOffset;
                    return result;
                }
            }

            throw new InvalidOperationException(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, offset));
        }

        public ValueParserDefinition ValueParser { get; private init; }

        public string? ParameterName { get; private init; }

        public bool IsOptional { get; private init; }
    }
}
