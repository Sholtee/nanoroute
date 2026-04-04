/********************************************************************************
* UriSegment.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;

namespace NanoRoute.Internals
{
    internal sealed class UriSegment
    {
        public const char SEPARATOR = '/';

        public readonly string _original;

        private readonly int _next;

        private UriSegment(string original, int start)
        {
            _original = original;

            while (start < original.Length && original[start] == SEPARATOR)
                start++;

            if (start >= original.Length)
            {
                Value = null;
                _next = -1;
                return;
            }

            int i = original.IndexOf(SEPARATOR, start);
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

        public UriSegment(string original) : this(original, 0) { }

        public string? Value { get; }

        public UriSegment? Next
        {
            get
            {
                if (field is null && _next > 0)
                    field = new UriSegment(_original, _next);
                return field;
            }
        }

        public IEnumerable<string> Enumerate()
        {
            for (UriSegment? current = this; current?.Value is not null; current = current.Next)
                yield return current.Value;
        }
    }
}