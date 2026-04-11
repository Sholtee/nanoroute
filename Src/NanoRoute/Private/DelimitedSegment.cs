/********************************************************************************
* DelimitedSegment.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace NanoRoute.Internals
{
    internal struct DelimitedSegment(string original, char separator)
    {
        private int _next;

        public bool MoveNext()
        {
            if (_next < 0)
            {
                Current = default;
                return false;
            }

            while (_next < original.Length && original[_next] == separator)
                _next++;

            if (_next >= original.Length)
            {
                Current = default;
                _next = -1;
                return false;
            }

            int i = original.IndexOf(separator, _next);
            if (i < 0)
            {
                Current = original.AsMemory(_next);
                _next = -1;
            }
            else
            {
                Current = original.AsMemory(_next, i - _next);
                _next = i + 1;
            }

            return true;
        }

        public ReadOnlyMemory<char> Current { get; private set; }

        public readonly bool HasValue => !Current.Equals(default);
    }
}
