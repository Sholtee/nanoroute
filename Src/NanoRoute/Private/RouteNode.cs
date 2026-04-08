/********************************************************************************
* RouteNode.cs                                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace NanoRoute.Internals
{
    /// <summary>
    /// Represents a node in the per-verb route tree.
    /// </summary>
    internal sealed class RouteNode
    {
        /// <summary>
        /// Gets the handlers registered for the current route node.
        /// </summary>
        public Dictionary<HttpVerb, List<HandlerRegistration>> HandlerRegistrations { get; } = [];

        /// <summary>
        /// Gets or sets the parser used by this node when it represents a parameterized segment.
        /// </summary>
        public SegmentParser? SegmentParser { get; init; }

        /// <summary>
        /// Gets or sets the segment for which this node is created
        /// </summary>
        public required ReadOnlyMemory<char> Segment { get; init; }

        /// <summary>
        /// Gets literal child nodes keyed by case-insensitive segment value.
        /// </summary>
        public Dictionary<ReadOnlyMemory<char>, RouteNode> LiteralChildren { get; } = new(ReadOnlyMemoryCharComparer.Instance);

        /// <summary>
        /// Gets parser-based child nodes evaluated after literal matches.
        /// </summary>
        public List<RouteNode> ParsedChildren { get; } = [];

        /// <summary>
        /// Creates a deep-copy from this node.
        /// </summary>
        public RouteNode Copy()
        {
            RouteNode result = new()
            {
                SegmentParser = SegmentParser,
                Segment = Segment
            };

            CopyCollection(ParsedChildren,        result.ParsedChildren,        static c => c.Copy());
            CopyCollection(HandlerRegistrations,  result.HandlerRegistrations,  static kvp => new(kvp.Key, [..kvp.Value]));
            CopyCollection(LiteralChildren,       result.LiteralChildren,       static kvp => new(kvp.Key, kvp.Value.Copy()));

            return result;

            static void CopyCollection<TValue>(ICollection<TValue> src, ICollection<TValue> dst, Func<TValue, TValue> valueCopy)
            {
                foreach(TValue item in src)
                    dst.Add(valueCopy(item));
            }
        }
    }
}