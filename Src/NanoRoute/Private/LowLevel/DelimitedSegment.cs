/********************************************************************************
* DelimitedSegment.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;

namespace NanoRoute.Internals
{
    internal struct DelimitedSegment(ReadOnlyMemory<char> original, char separator): IEnumerator<ReadOnlyMemory<char>>
    {
        private const int DONE = -1;

        private int _next;

        public bool MoveNext()
        {
            if (_next is DONE)
            {
                Current = default;
                return false;
            }

            ReadOnlySpan<char> span = Original.Span;

            while (_next < span.Length && span[_next] == Separator)
                _next++;

            if (_next >= span.Length)
            {
                Current = default;
                _next = DONE;
                return false;
            }

            int i = span.Slice(_next).IndexOf(Separator);
            if (i < 0)
            {
                Current = Original.Slice(_next);
                _next = DONE;
            }
            else
            {
                Current = Original.Slice(_next, i);
                _next += i + 1;
            }

            return true;
        }

        public void Reset()
        {
            _next = 0;
            Current = default;
        }

        public void Dispose() {}

        public ReadOnlyMemory<char> Current { get; private set; }

        readonly object IEnumerator.Current => Current;

        public readonly ReadOnlyMemory<char> Remaining => _next > DONE ? Original.Slice(Math.Max(0, _next - 1)) : default;

        public ReadOnlyMemory<char> Original { get; } = original;

        public char Separator { get; } = separator;

        public readonly bool HasValue => Current.Length > 0;  
    }
}
