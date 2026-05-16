/********************************************************************************
* RouteNode.cs                                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

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
        public IDictionary<HttpVerb, IList<HandlerRegistration>> HandlerRegistrations { get; }

        /// <summary>
        /// Gets the parser used by this node when it represents a parameterized segment.
        /// </summary>
        public ParameterParser? ParameterParser { get; init; }

        /// <summary>
        /// Gets literal child nodes keyed by case-insensitive segment value.
        /// </summary>
        public IDictionary<ReadOnlyMemory<char>, RouteNode> LiteralChildren { get; }

        /// <summary>
        /// Gets parser-based child nodes evaluated after literal matches.
        /// </summary>
        public IList<RouteNode> ParsedChildren { get; }

        /// <summary>
        /// Returns true if this node is read-only.
        /// </summary>
        public bool Frozen { get; }

        public RouteNode()
        {
            HandlerRegistrations = new Dictionary<HttpVerb, IList<HandlerRegistration>>();
            LiteralChildren = new Dictionary<ReadOnlyMemory<char>, RouteNode>(ReadOnlyMemoryCharComparer.Instance);
            ParsedChildren = new List<RouteNode>();
        }

        private RouteNode(RouteNode src, bool freeze)
        {
            ParameterParser = src.ParameterParser;

            if (!freeze)
            {
                CopyCollection
                (
                    src.ParsedChildren,
                    ParsedChildren = new List<RouteNode>(src.ParsedChildren.Count),
                    static c => c.Copy(freeze: false)
                );

                CopyCollection
                (
                    src.HandlerRegistrations,
                    HandlerRegistrations = new Dictionary<HttpVerb, IList<HandlerRegistration>>(src.HandlerRegistrations.Count),
                    static kvp => new(kvp.Key, kvp.Value.ToList())
                );

                CopyCollection
                (
                    src.LiteralChildren,
                    LiteralChildren = new Dictionary<ReadOnlyMemory<char>, RouteNode>(src.LiteralChildren.Count, ReadOnlyMemoryCharComparer.Instance),
                    static kvp => new(kvp.Key, kvp.Value.Copy(freeze: false))
                );

                static void CopyCollection<TValue>(ICollection<TValue> src, ICollection<TValue> dst, Func<TValue, TValue> valueCopy)
                {
                    foreach (TValue item in src)
                        dst.Add(valueCopy(item));
                }
            }
            else
            {
                LiteralChildren = src.LiteralChildren.ToFrozenDictionary
                (
                    static kvp => kvp.Key,
                    static kvp => kvp.Value.Copy(freeze: true),
                    ReadOnlyMemoryCharComparer.Instance
                );

                ParsedChildren = src.ParsedChildren
                    .Select(static node => node.Copy(freeze: true))
                    .ToImmutableArray();

                HandlerRegistrations = src
                    .HandlerRegistrations
                    .ToFrozenDictionary
                    (
                        static kvp => kvp.Key,
                        static kvp => (IList<HandlerRegistration>)kvp.Value.ToImmutableArray()
                    );

                Frozen = true;
            }
        }

        /// <summary>
        /// Creates a deep-copy from this node.
        /// </summary>
        public RouteNode Copy(bool freeze) => new(this, freeze);
    }
}
