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

        public bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y) => x.Span.Equals(y.Span, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ReadOnlyMemory<char> obj)
        {
            ReadOnlySpan<char> input = obj.Span;

            Span<char> buffer = stackalloc char[4];

            // https://github.com/bryc/code/blob/master/jshash/hashes/murmurhash3.js
            unchecked
            {
                uint h = (uint) 1986, k;

                int i = 0;

                for (int b = obj.Length & -4; i < b; i += 4)
                {
                    ReadOnlySpan<char> block = BlockToUpper(input.Slice(i, 4), buffer);

                    k = (uint) (block[3] << 24 | block[2] << 16 | block[1] << 8 | block[0]);
                    k *= 3432918353; k = k << 15 | k >> 17;
                    h ^= k * 461845907; h = h << 13 | h >> 19;
                    h = h * 5 + 3864292196;
                }

                int m = input.Length & 3;
                if (m > 0)
                {
                    k = 0;
                    switch (m)
                    {
                        case 3:
                            k ^= (uint) CharToUpper(input[i + 2]) << 16;
                            goto case 2;
                        case 2:
                            k ^= (uint) CharToUpper(input[i + 1]) << 8;
                            goto case 1;
                        case 1:
                            k ^= (uint) CharToUpper(input[i]);
                            k *= 3432918353; k = k << 15 | k >> 17;
                            h ^= k * 461845907;
                            break;
                    }
                }

                h ^= (uint) input.Length;

                h ^= h >> 16; h *= 2246822507;
                h ^= h >> 13; h *= 3266489909;
                h ^= h >> 16;

                return (int) h;
            }

            // https://github.com/dotnet/runtime/blob/ecc8cb5bc0411e0fb0549230f70dfe8ab302c65c/src/libraries/System.Private.CoreLib/src/System/Text/Unicode/Utf16Utility.cs#L98
            static ReadOnlySpan<char> BlockToUpper(ReadOnlySpan<char> chars, Span<char> buffer)
            {
                ulong l = Unsafe.As<char, ulong>(ref MemoryMarshal.GetReference(chars));

                if ((l & ~0x007F_007F_007F_007Ful) is 0)
                {
                    // All the 4 chars are ASCII
                    ulong
                        lowerIndicator = l + 0x0080_0080_0080_0080ul - 0x0061_0061_0061_0061ul,
                        upperIndicator = l + 0x0080_0080_0080_0080ul - 0x007B_007B_007B_007Bul,
                        combinedIndicator = lowerIndicator ^ upperIndicator,
                        mask = (combinedIndicator & 0x0080_0080_0080_0080ul) >> 2;

                    Unsafe.As<char, ulong>(ref MemoryMarshal.GetReference(buffer)) = l ^ mask;
                }
                else
                {
                    // Slow like hell
                    chars.ToUpperInvariant(buffer);
                }
                return buffer;
            }

            static char CharToUpper(char chr)
            {
                if ((chr & ~0x007Fu) is 0)
                {
                    uint
                        lowerIndicator = chr + 0x0080u - 0x0061u,
                        upperIndicator = chr + 0x0080u - 0x007Bu,
                        combinedIndicator = lowerIndicator ^ upperIndicator,
                        mask = (combinedIndicator & 0x0080u) >> 2;

                    return (char)(chr ^ mask);
                }

                // Slow...
                return char.ToUpperInvariant(chr);
            }
        }
    }
}