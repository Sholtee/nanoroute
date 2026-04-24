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
            if (x.Length != y.Length)
                return false;

            ref char
                leftRef = ref MemoryMarshal.GetReference(x.Span),
                rightRef = ref MemoryMarshal.GetReference(y.Span);

            for (int i = 0; i < y.Length; i++)
                if (CharToUpper(Unsafe.Add(ref leftRef, i)) != CharToUpper(Unsafe.Add(ref rightRef, i)))
                    return false;

            return true;
        }

        public int GetHashCode(ReadOnlyMemory<char> obj)
        {
            ref char inputRef = ref MemoryMarshal.GetReference(obj.Span);

            // https://github.com/bryc/code/blob/master/jshash/hashes/murmurhash3.js
            unchecked
            {
                uint h = (uint) 1986, k;

                int i = 0;

                for (int b = obj.Length & -4; i < b; i += 4)
                {
                    ulong
                        block = Unsafe.As<char, ulong>(ref Unsafe.Add(ref inputRef, i)),
                        upperBlock = BlockToUpper(block);

                    ref char upperBlockRef = ref Unsafe.As<ulong, char>(ref upperBlock);

                    k = (uint) (Unsafe.Add(ref upperBlockRef, 3) << 24 | Unsafe.Add(ref upperBlockRef, 2) << 16 | Unsafe.Add(ref upperBlockRef, 1) << 8 | upperBlockRef);
                    k *= 3432918353; k = k << 15 | k >> 17;
                    h ^= k * 461845907; h = h << 13 | h >> 19;
                    h = h * 5 + 3864292196;
                }

                int m = obj.Length & 3;
                if (m > 0)
                {
                    k = 0;
                    switch (m)
                    {
                        case 3:
                            k ^= (uint) CharToUpper(Unsafe.Add(ref inputRef, i + 2)) << 16;
                            goto case 2;
                        case 2:
                            k ^= (uint) CharToUpper(Unsafe.Add(ref inputRef, i + 1)) << 8;
                            goto case 1;
                        case 1:
                            k ^= (uint) CharToUpper(Unsafe.Add(ref inputRef, i));
                            k *= 3432918353; k = k << 15 | k >> 17;
                            h ^= k * 461845907;
                            break;
                    }
                }

                h ^= (uint) obj.Length;

                h ^= h >> 16; h *= 2246822507;
                h ^= h >> 13; h *= 3266489909;
                h ^= h >> 16;

                return (int) h;
            }

            // https://github.com/dotnet/runtime/blob/ecc8cb5bc0411e0fb0549230f70dfe8ab302c65c/src/libraries/System.Private.CoreLib/src/System/Text/Unicode/Utf16Utility.cs#L98
            static ulong BlockToUpper(ulong input)
            {
                if ((input & ~0x007F_007F_007F_007Ful) is 0)
                {
                    // All the 4 chars are ASCII
                    ulong
                        lowerIndicator = input + 0x0080_0080_0080_0080ul - 0x0061_0061_0061_0061ul,
                        upperIndicator = input + 0x0080_0080_0080_0080ul - 0x007B_007B_007B_007Bul,
                        combinedIndicator = lowerIndicator ^ upperIndicator,
                        mask = (combinedIndicator & 0x0080_0080_0080_0080ul) >> 2;

                    return input ^ mask;
                }
                else
                {
                    ref char inputRef = ref Unsafe.As<ulong, char>(ref input);

                    return
                        (ulong) CharToUpper(inputRef) |
                        ((ulong) CharToUpper(Unsafe.Add(ref inputRef, 1)) << 16) |
                        ((ulong) CharToUpper(Unsafe.Add(ref inputRef, 2)) << 32) |
                        ((ulong) CharToUpper(Unsafe.Add(ref inputRef, 3)) << 48);
                }
            }
        }

        private static char CharToUpper(char chr)
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

            // Slow...
            return char.ToUpperInvariant(chr);
        }
    }
}
