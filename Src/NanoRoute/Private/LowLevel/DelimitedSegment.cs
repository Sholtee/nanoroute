/********************************************************************************
* DelimitedSegment.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace NanoRoute.Internals
{
    internal struct DelimitedSegment(ReadOnlyMemory<char> original, char separator)
    {
        private const int DONE = -1;

        private int _next;

        public bool MoveNext()
        {
            ReadOnlySpan<char> span = original.Span;

            if (_next is DONE)
            {
                Current = default;
                return false;
            }

            while (_next < span.Length && span[_next] == separator)
                _next++;

            if (_next >= span.Length)
            {
                Current = default;
                _next = DONE;
                return false;
            }

            int i = span.Slice(_next).IndexOf(separator);
            if (i < 0)
            {
                Current = original.Slice(_next);
                _next = DONE;
            }
            else
            {
                Current = original.Slice(_next, i);
                _next += i + 1;
            }

            return true;
        }

        public ReadOnlyMemory<char> Current { get; private set; }

        public readonly bool HasValue => Current.Length > 0;
    }
}
