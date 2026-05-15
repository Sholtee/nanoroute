/********************************************************************************
* ParameterDefinition.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NanoRoute.Internals
{
    using Properties;

    internal readonly struct ParameterDefinition
    {
        private const string
            PARAMETER_NAME = $@"\w+\??",
            PARAMETER_NAME_PATTERN = $@"\G(?:(?<parameterName>{PARAMETER_NAME}):)?";

        private static readonly Regex s_parameterName = new(PARAMETER_NAME_PATTERN, RuntimeFeature.IsDynamicCodeSupported ? RegexOptions.Compiled : RegexOptions.None);

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
                    IsOptional = parameterName.EndsWith
                    (
#if NETSTANDARD2_1_OR_GREATER
                        '?'
#else
                        "?"
#endif
                    )
                };

                if (newOffset < pattern.Length && pattern[newOffset] == '}')
                {
                    offset = newOffset;
                    return result;
                }
            }

            throw new ArgumentException(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, offset), nameof(pattern));
        }

        public required ValueParserDefinition ValueParser { get; init; }

        public string? ParameterName { get; init; }

        public bool IsOptional { get; init; }

        public int Index { get; init; }
    }
}
