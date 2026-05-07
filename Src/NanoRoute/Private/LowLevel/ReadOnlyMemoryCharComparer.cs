/********************************************************************************
* ReadOnlyMemoryCharComparer.cs                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NanoRoute.Internals
{
    internal sealed class ReadOnlyMemoryCharComparer: IEqualityComparer<ReadOnlyMemory<char>>
    {
        public static ReadOnlyMemoryCharComparer Instance { get; } = new();

        public bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
        {
            int length = x.Length;
            if (length != y.Length)
                return false;

            ref char
                leftRef = ref MemoryMarshal.GetReference(x.Span),
                rightRef = ref MemoryMarshal.GetReference(y.Span);

            int i = 0;

            for (int bulkLength = length & -4; i < bulkLength; i += 4)
            {
                ulong
                    leftBlock = Unsafe.As<char, ulong>(ref Unsafe.Add(ref leftRef, i)),
                    rightBlock = Unsafe.As<char, ulong>(ref Unsafe.Add(ref rightRef, i));

                if (BlockToUpper(leftBlock) != BlockToUpper(rightBlock))
                    return false;
            }

            int remainingChars = length & 3;

            switch (remainingChars)
            {
                case 3:
                    if (CharToUpper(Unsafe.Add(ref leftRef, i + 2)) != CharToUpper(Unsafe.Add(ref rightRef, i + 2)))
                        return false;
                    goto case 2;
                case 2:
                    if (CharToUpper(Unsafe.Add(ref leftRef, i + 1)) != CharToUpper(Unsafe.Add(ref rightRef, i + 1)))
                        return false;
                    goto case 1;
                case 1:
                    if (CharToUpper(Unsafe.Add(ref leftRef, i)) != CharToUpper(Unsafe.Add(ref rightRef, i)))
                        return false;
                    break;
            }

            return true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static char CharToUpper(char chr)
            {
                if ((chr & ~0x007Fu) is 0)
                {
                    uint
                        lowerIndicator = chr + 0x0080u - 0x0061u,
                        upperIndicator = chr + 0x0080u - 0x007Bu,
                        combinedIndicator = lowerIndicator ^ upperIndicator,
                        mask = (combinedIndicator & 0x0080u) >> 2;

                    return (char) (chr ^ mask);
                }

                return CharToUpperNonAscii(chr);
            }
        }

        public int GetHashCode(ReadOnlyMemory<char> obj)
        {
            ref char inputRef = ref MemoryMarshal.GetReference(obj.Span);

            unchecked
            {
                uint
                    p0 = 0xD6E8_FEB8u,
                    p1 = 0xA5A5_A5A5u;

                int i = 0;

                for (int bulkLength = obj.Length & -4; i < bulkLength; i += 4)
                {
                    ulong
                        block = Unsafe.As<char, ulong>(ref Unsafe.Add(ref inputRef, i)),
                        upperBlock = BlockToUpper(block);

                    // Feed Marvin with the uppercased UTF-16 bytes, two chars at a time.
                    ref uint upperBlockRef = ref Unsafe.As<ulong, uint>(ref upperBlock);

                    p0 += upperBlockRef;
                    MarvinBlock(ref p0, ref p1);

                    p0 += Unsafe.Add(ref upperBlockRef, 1);
                    MarvinBlock(ref p0, ref p1);
                }

                int remainingChars = obj.Length & 3;

                if (remainingChars is 0)
                    p0 += 0x80u;
                else
                {
                    // Pack the 1-3 char tail into a local block so BlockToUpper can handle it too.
                    ulong tail = 0;

                    switch (remainingChars)
                    {
                        case 3:
                            tail |= (ulong) Unsafe.Add(ref inputRef, i + 2) << 32;
                            goto case 2;
                        case 2:
                            tail |= (ulong) Unsafe.Add(ref inputRef, i + 1) << 16;
                            goto case 1;
                        case 1:
                            tail |= Unsafe.Add(ref inputRef, i);
                            break;
                    }

                    tail = BlockToUpper(tail);

                    ref char tailRef = ref Unsafe.As<ulong, char>(ref tail);
                    // A 2- or 3-char tail has one complete Marvin block before padding.
                    if (remainingChars >= 2)
                    {
                        p0 += Unsafe.As<char, uint>(ref tailRef);
                        MarvinBlock(ref p0, ref p1);
                    }

                    // Add Marvin's final 0x80 padding after the remaining UTF-16 bytes.
                    p0 += remainingChars is 2
                        ? 0x80u
                        : (uint) Unsafe.Add(ref tailRef, remainingChars - 1) | 0x0080_0000u;
                }

                MarvinBlock(ref p0, ref p1);
                MarvinBlock(ref p0, ref p1);

                return (int) (p1 ^ p0);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void MarvinBlock(ref uint p0, ref uint p1)
            {
                p1 ^= p0;
                p0 = RotateLeft(p0, 20);
                p0 += p1;
                p1 = RotateLeft(p1, 9);
                p1 ^= p0;
                p0 = RotateLeft(p0, 27);
                p0 += p1;
                p1 = RotateLeft(p1, 19);

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static uint RotateLeft(uint value, int offset) => value << offset | value >> (32 - offset);
            }
        }

        // https://github.com/dotnet/runtime/blob/ecc8cb5bc0411e0fb0549230f70dfe8ab302c65c/src/libraries/System.Private.CoreLib/src/System/Text/Unicode/Utf16Utility.cs#L98
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong BlockToUpper(ulong input)
        {
            ulong
                // Each 16-bit lane is non-zero here only when that char is outside ASCII.
                nonAsciiMask = input & ~0x007F_007F_007F_007Ful,
                lowerIndicator = input + 0x0080_0080_0080_0080ul - 0x0061_0061_0061_0061ul,
                upperIndicator = input + 0x0080_0080_0080_0080ul - 0x007B_007B_007B_007Bul,
                combinedIndicator = lowerIndicator ^ upperIndicator,
                asciiUpper = input ^ ((combinedIndicator & 0x0080_0080_0080_0080ul) >> 2);

            if (nonAsciiMask is not 0)
            {
                // ASCII lanes are already uppercased; only non-ASCII lanes need the slow path.
                ref char inputRef = ref Unsafe.As<ulong, char>(ref input);
                ref char resultRef = ref Unsafe.As<ulong, char>(ref asciiUpper);

                if ((nonAsciiMask & 0xFF80ul) is not 0)
                    resultRef = CharToUpperNonAscii(inputRef);

                if ((nonAsciiMask & 0xFF80_0000ul) is not 0)
                    Unsafe.Add(ref resultRef, 1) = CharToUpperNonAscii(Unsafe.Add(ref inputRef, 1));

                if ((nonAsciiMask & 0xFF80_0000_0000ul) is not 0)
                    Unsafe.Add(ref resultRef, 2) = CharToUpperNonAscii(Unsafe.Add(ref inputRef, 2));

                if ((nonAsciiMask & 0xFF80_0000_0000_0000ul) is not 0)
                    Unsafe.Add(ref resultRef, 3) = CharToUpperNonAscii(Unsafe.Add(ref inputRef, 3));
            }

            return asciiUpper;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char CharToUpperNonAscii(char chr)
        {
            uint value = chr;

            // UnicodeData.txt defines these Latin-1 letters as simple one-to-one
            // uppercase pairs. The lowercase ranges map by subtracting 0x20, while
            // the matching uppercase ranges are already normalized. The split ranges
            // intentionally skip multiplication and division signs.
            if ((value - 0x00C0u) <= 0x0016u || (value - 0x00D8u) <= 0x0006u)
                return chr;

            if ((value - 0x00E0u) <= 0x0016u || (value - 0x00F8u) <= 0x0006u)
                return (char) (chr - 0x20);

            return char.ToUpperInvariant(chr);
        }
    }
}
