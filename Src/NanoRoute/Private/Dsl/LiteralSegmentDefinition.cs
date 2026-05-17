/********************************************************************************
* LiteralSegmentDefinition.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NanoRoute.Internals
{
    using Properties;

    internal static class LiteralSegmentDefinition
    {
        private const string
            URI_CHAR = @"[\w.\-~+]",
            PERCENT_ENCODED_CHAR = "%[0-9A-Fa-f]{2}",
            LITERAL_SEGMENT_PATTERN = $@"\G(?:{URI_CHAR}|{PERCENT_ENCODED_CHAR})+";

        private static readonly Regex s_literalSegment = new(LITERAL_SEGMENT_PATTERN, RuntimeFeature.IsDynamicCodeSupported ? RegexOptions.Compiled : RegexOptions.None);

        public static ReadOnlyMemory<char> Parse(string pattern, ref int offset)
        {
            if (s_literalSegment.Match(pattern, offset) is not { Success: true, Length: int length })
                throw new ArgumentException(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, offset), nameof(pattern));

            ReadOnlyMemory<char> segment = UrlUtils.DecodeUrl(pattern.AsMemory(offset, length), UrlDecodeMode.Path);

            offset += length - 1;
            return segment;
        }
    }
}
