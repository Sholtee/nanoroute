/********************************************************************************
* UrlUtils.cs                                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Text;

namespace NanoRoute.Internals
{
    using Properties;

    internal enum UrlDecodeMode
    {
        Path,
        Form
    }

    internal static class UrlUtils
    {
        private static readonly Encoding s_utf8 = new UTF8Encoding(false, true);

        public static ReadOnlyMemory<char> DecodeUrl(ReadOnlyMemory<char> source, UrlDecodeMode mode)
        {
            char[] result = new char[source.Length];

            if (!TryDecodeUrl(source.Span, result.AsSpan(), mode, out int charsWritten))
                throw new InvalidOperationException(Resources.ERR_DECODING_FAILED);

            return result.AsMemory(0, charsWritten);
        }

        public static bool TryDecodeUrl(ReadOnlySpan<char> source, Span<char> destination, UrlDecodeMode mode, out int charsWritten)
        {
            charsWritten = 0;

            for (int i = 0; i < source.Length;)
            {
                if (charsWritten >= destination.Length)
                    return false;

                switch (source[i])
                {
                    case '+' when mode is UrlDecodeMode.Form:
                        destination[charsWritten++] = ' ';
                        i++;
                        break;

                    case '%':
                        if (!TryDecodeUtf8Sequence(source, ref i, destination, ref charsWritten))
                            return false;
                        break;

                    default:
                        if (!TryCopyLiteralSegment(source, mode, ref i, destination, ref charsWritten))
                            return false;
                        break;
                }
            }

            return true;
        }

        private static bool TryCopyLiteralSegment(ReadOnlySpan<char> source, UrlDecodeMode mode, ref int offset, Span<char> destination, ref int charsWritten)
        {
            ReadOnlySpan<char> segment = source.Slice(offset);

            int special = mode is UrlDecodeMode.Form ? segment.IndexOfAny('%', '+') : segment.IndexOf('%');
            if (special > 0)
                segment = segment.Slice(0, special);

            if (charsWritten + segment.Length > destination.Length)
                return false;

            segment.CopyTo(destination.Slice(charsWritten));
            charsWritten += segment.Length;
            offset += segment.Length;

            return true;
        }

        private static bool TryDecodeUtf8Sequence(ReadOnlySpan<char> source, ref int offset, Span<char> destination, ref int charsWritten)
        {
            const int ESCAPED_BYTE_LENGTH = 3; // "%XX"
#if NETSTANDARD2_1_OR_GREATER
            Span<byte> bytes = stackalloc byte[4];
#else
            byte[] bytes = new byte[4];
#endif
            // The first escaped byte determines how many %XX chunks belong to this UTF-8 sequence.
            if (!TryParseEscapedByte(source, offset, out bytes[0]))
                return false;

            int byteCount = GetUtf8ByteCount(bytes[0]);
            if (byteCount < 0)
                return false;

            // Collect the remaining escaped bytes, then let the strict UTF-8 decoder validate them.
            for (int i = 1; i < byteCount; i++)
                if (!TryParseEscapedByte(source, offset + i * ESCAPED_BYTE_LENGTH, out bytes[i]))
                    return false;

#if NETSTANDARD2_1_OR_GREATER
            Span<char> chars = stackalloc char[2];
#else
            char[] chars = new char[2];
#endif
            int decodedChars;

            try
            {
#if NETSTANDARD2_1_OR_GREATER
                decodedChars = s_utf8.GetChars(bytes.Slice(0, byteCount), chars);
#else
                decodedChars = s_utf8.GetChars(bytes, 0, byteCount, chars, 0);
#endif
            }
            catch (DecoderFallbackException)
            {
                return false;
            }

            if (charsWritten + decodedChars > destination.Length)
                return false;

            chars
#if NETSTANDARD2_0
                .AsSpan(0, decodedChars)
#endif
                .CopyTo(destination.Slice(charsWritten));
            charsWritten += decodedChars;
            offset += byteCount * ESCAPED_BYTE_LENGTH;

            return true;
        }

        private static bool TryParseEscapedByte(ReadOnlySpan<char> source, int offset, out byte value)
        {
            value = default;

            return offset >= 0 &&
                   offset + 2 < source.Length &&
                   source[offset] is '%' &&
                   TryParseHex2(source[offset + 1], source[offset + 2], out value);
        }

        private static int GetUtf8ByteCount(byte first) => first switch
        {
            <= 0x7F => 1,
            >= 0xC2 and <= 0xDF => 2,
            >= 0xE0 and <= 0xEF => 3,
            >= 0xF0 and <= 0xF4 => 4,
            _ => -1
        };

        private static bool TryParseHex2(char high, char low, out byte value)
        {
            value = default;

            int
                hi = HexToInt(high),
                lo = HexToInt(low);

            if (hi < 0 || lo < 0)
                return false;

            value = (byte) ((hi << 4) | lo);
            return true;

            static int HexToInt(char c)
            {
                if ((uint)(c - '0') <= 9) return c - '0';
                if ((uint)(c - 'a') <= 5) return c - 'a' + 10;
                if ((uint)(c - 'A') <= 5) return c - 'A' + 10;
                return -1;
            }
        }
    }
}

