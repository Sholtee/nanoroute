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

            switch (length & 3)
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
        }

        public int GetHashCode(ReadOnlyMemory<char> obj)
        {
            ref char inputRef = ref MemoryMarshal.GetReference(obj.Span);

            // https://github.com/bryc/code/blob/master/jshash/hashes/murmurhash3.js
            unchecked
            {
                uint hash = 1986, blockHash;

                int i = 0;

                for (int bulkLength = obj.Length & -4; i < bulkLength; i += 4)
                {
                    ulong
                        block = Unsafe.As<char, ulong>(ref Unsafe.Add(ref inputRef, i)),
                        upperBlock = BlockToUpper(block);

                    ref char upperBlockRef = ref Unsafe.As<ulong, char>(ref upperBlock);

                    blockHash = (uint) (Unsafe.Add(ref upperBlockRef, 3) << 24 | Unsafe.Add(ref upperBlockRef, 2) << 16 | Unsafe.Add(ref upperBlockRef, 1) << 8 | upperBlockRef);
                    blockHash *= 3432918353; blockHash = blockHash << 15 | blockHash >> 17;
                    hash ^= blockHash * 461845907; hash = hash << 13 | hash >> 19;
                    hash = hash * 5 + 3864292196;
                }

                int remainingChars = obj.Length & 3;
                if (remainingChars > 0)
                {
                    blockHash = 0;
                    switch (remainingChars)
                    {
                        case 3:
                            blockHash ^= (uint) CharToUpper(Unsafe.Add(ref inputRef, i + 2)) << 16;
                            goto case 2;
                        case 2:
                            blockHash ^= (uint) CharToUpper(Unsafe.Add(ref inputRef, i + 1)) << 8;
                            goto case 1;
                        case 1:
                            blockHash ^= (uint) CharToUpper(Unsafe.Add(ref inputRef, i));
                            blockHash *= 3432918353; blockHash = blockHash << 15 | blockHash >> 17;
                            hash ^= blockHash * 461845907;
                            break;
                    }
                }

                hash ^= (uint) obj.Length;

                hash ^= hash >> 16; hash *= 2246822507;
                hash ^= hash >> 13; hash *= 3266489909;
                hash ^= hash >> 16;

                return (int) hash;
            }
        }

        // https://github.com/dotnet/runtime/blob/ecc8cb5bc0411e0fb0549230f70dfe8ab302c65c/src/libraries/System.Private.CoreLib/src/System/Text/Unicode/Utf16Utility.cs#L98
        private static ulong BlockToUpper(ulong input)
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
