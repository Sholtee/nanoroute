/********************************************************************************
* ReadOnlyMemoryCharComparer.cs                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace NanoRoute.Internals
{
    internal sealed class ReadOnlyMemoryCharComparer: IEqualityComparer<ReadOnlyMemory<char>>
    {
        public static ReadOnlyMemoryCharComparer Instance { get; } = new();

        public bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y) => x.Span.Equals(y.Span, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ReadOnlyMemory<char> obj)
        {
            var span = obj.Span;

            unchecked
            {
                int hash = 17;

                for (int i = 0; i < span.Length; i++)
                {
                    hash = (hash * 31) + ToUpperAscii(span[i]);
                }

                return hash;
            }
        }

        // Enough since URIs contain ASCII characters only
        private static char ToUpperAscii(char c) => (c >= 'a' && c <= 'z') ? (char)(c - 32) : c;
    }
}