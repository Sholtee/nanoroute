/********************************************************************************
* StringSegment.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;

namespace NanoRoute.Internals
{
    internal sealed class StringSegment
    {
        public readonly string _original;

        private readonly int _next;

        private readonly char _split;

        private StringSegment(string original, int start, char split)
        {
            _original = original;
            _split = split;

            while (start < original.Length && original[start] == split)
                start++;

            if (start >= original.Length)
            {
                Value = null;
                _next = -1;
                return;
            }

            int i = original.IndexOf(split, start);
            if (i < 0)
            {
                Value = original.Substring(start);
                _next = -1;
            }
            else
            {
                Value = original.Substring(start, i - start);
                _next = i + 1;
            }
        }

        public StringSegment(string original, char split) : this(original, 0, split) { }

        public string? Value { get; }

        public StringSegment? Next
        {
            get
            {
                if (field is null && _next > 0)
                    field = new StringSegment(_original, _next, _split);
                return field;
            }
        }

        public IEnumerable<string> Enumerate()
        {
            for (StringSegment? current = this; current?.Value is not null; current = current.Next)
                yield return current.Value;
        }
    }
}