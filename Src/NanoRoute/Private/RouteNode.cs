/********************************************************************************
* RouteNode.cs                                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace NanoRoute.Internals
{
    /// <summary>
    /// Represents a node in the per-verb route tree.
    /// </summary>
    internal sealed class RouteNode()
    {
        /// <summary>
        /// Gets the handlers registered for the current route node.
        /// </summary>
        public IDictionary<HttpVerb, IList<HandlerRegistration>> HandlerRegistrations { get; } = new Dictionary<HttpVerb, IList<HandlerRegistration>>();

        /// <summary>
        /// Gets the parser used by this node when it represents a parameterized segment.
        /// </summary>
        public ParameterParser? ParameterParser { get; init; }

        /// <summary>
        /// Gets literal child nodes keyed by case-insensitive segment value.
        /// </summary>
        public IDictionary<ReadOnlyMemory<char>, RouteNode> LiteralChildren { get; } = new Dictionary<ReadOnlyMemory<char>, RouteNode>(ReadOnlyMemoryCharComparer.Instance);

        /// <summary>
        /// Gets parser-based child nodes evaluated after literal matches.
        /// </summary>
        public IList<RouteNode> ParsedChildren { get; } = new List<RouteNode>();

        /// <summary>
        /// Returns true if this node is read-only.
        /// </summary>
        public bool Frozen { get; }

        private RouteNode(RouteNode src, bool freeze): this()
        {
            ParameterParser = src.ParameterParser;

            CopyCollection(src.ParsedChildren, ParsedChildren, c => c.Copy(freeze));
            CopyCollection(src.HandlerRegistrations, HandlerRegistrations, static kvp => new(kvp.Key, [.. kvp.Value]));
            CopyCollection(src.LiteralChildren, LiteralChildren, kvp => new(kvp.Key, kvp.Value.Copy(freeze)));

            if (freeze)
            {
                LiteralChildren = LiteralChildren.ToFrozenDictionary(ReadOnlyMemoryCharComparer.Instance);
                ParsedChildren = ParsedChildren.ToImmutableArray();
                HandlerRegistrations = HandlerRegistrations.ToFrozenDictionary(static kvp => kvp.Key, static kvp => (IList<HandlerRegistration>) kvp.Value.ToImmutableArray());
                Frozen = true;
            }

            static void CopyCollection<TValue>(ICollection<TValue> src, ICollection<TValue> dst, Func<TValue, TValue> valueCopy)
            {
                foreach (TValue item in src)
                    dst.Add(valueCopy(item));
            }
        }

        /// <summary>
        /// Creates a deep-copy from this node.
        /// </summary>
        public RouteNode Copy(bool frozen) => new(this, frozen);
    }
}
