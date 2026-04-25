/********************************************************************************
* ValueParsers.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Runtime.CompilerServices;

namespace NanoRoute.Internals
{
    internal static class ValueParsers
    {
        public static bool TryParseInt32(ReadOnlyMemory<char> value, out int result)
        {
            ReadOnlySpan<char> span = value.Span;

            result = default;

            if (span.IsEmpty)
                return false;

            int index = 0;
            bool negative = false;

            switch (span[0])
            {
                case '-':
                    negative = true;
                    index = 1;
                    break;

                case '+':
                    index = 1;
                    break;
            }

            if (index >= span.Length)
                return false;

            int parsed = 0;
            int limit = negative ? int.MinValue : -int.MaxValue;
            int minQuotient = limit / 10;

            for (; index < span.Length; index++)
            {
                int digit = span[index] - '0';
                if ((uint) digit > 9)
                    return false;

                if (parsed < minQuotient)
                    return false;

                parsed *= 10;

                if (parsed < limit + digit)
                    return false;

                parsed -= digit;
            }

            result = negative ? parsed : -parsed;
            return true;
        }

        private static readonly char[]
            s_True = ['t', 'r', 'u', 'e'],
            s_False = ['f', 'a', 'l', 's', 'e'];

        public static bool TryParseBoolean(ReadOnlyMemory<char> value, out bool result)
        {
            result = false;

            ReadOnlySpan<char> span = value.Span;

            if (span.Equals(s_False, StringComparison.OrdinalIgnoreCase))
                return true;

            if (span.Equals(s_True, StringComparison.OrdinalIgnoreCase))
                return result = true;

            return false;
        }

        public static bool TryParseGuid(ReadOnlyMemory<char> value, out Guid result)
        {
            ReadOnlySpan<char> span = value.Span;

            result = default;

            if (span.Length is not 32 and not 36)
                return false;

            if (span.Length is 36 && (span[8] is not '-' || span[13] is not '-' || span[18] is not '-' || span[23] is not '-'))
                return false;

            int index = 0;

            if (!TryParseHexDigit(span, ref index, out int a)   ||
                !TryParseHexDigit(span, ref index, out short b) ||
                !TryParseHexDigit(span, ref index, out short c) ||
                !TryParseHexDigit(span, ref index, out byte d)  ||
                !TryParseHexDigit(span, ref index, out byte e)  ||
                !TryParseHexDigit(span, ref index, out byte f)  ||
                !TryParseHexDigit(span, ref index, out byte g)  ||
                !TryParseHexDigit(span, ref index, out byte h)  ||
                !TryParseHexDigit(span, ref index, out byte i)  ||
                !TryParseHexDigit(span, ref index, out byte j)  ||
                !TryParseHexDigit(span, ref index, out byte k))
                return false;

            result = new Guid(a, b, c, d, e, f, g, h, i, j, k);
            return true;
        }

        private static bool TryParseHexDigit<T>(ReadOnlySpan<char> source, ref int index, out T result) where T : struct
        {
            result = default;

            uint res = 0;

            for (int i = 0; i < Unsafe.SizeOf<T>() * 2; i++)
            {
                if (!TryGetHexDigit(source, ref index, out int digit))
                    return false;

                res = (res << 4) | (uint) digit;
            }

            result = Unsafe.As<uint, T>(ref res);
            return true;
        }

        private static bool TryGetHexDigit(ReadOnlySpan<char> source, ref int index, out int digit)
        {
            if (index < source.Length && source[index] is '-')
                index++;

            if (index >= source.Length)
            {
                digit = default;
                return false;
            }

            digit = HexToInt(source[index++]);
            return digit >= 0;

            static int HexToInt(char value)
            {
                if (value - '0' <= 9)
                    return value - '0';

                char lower = value - 'A' <= 25 ? (char)(value + ('a' - 'A')) : value;
                return lower - 'a' <= 5 ? lower - 'a' + 10 : -1;
            }
        }
    }
}
