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
    internal readonly union RouteSingleBranch(KeyValuePair<ReadOnlyMemory<char>, RouteNode>, KeyValuePair<ParameterParser, RouteNode>);

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
        /// Gets literal child nodes keyed by case-insensitive segment value.
        /// </summary>
        public IDictionary<ReadOnlyMemory<char>, RouteNode> LiteralChildren { get; }

        /// <summary>
        /// Gets parser-based child nodes.
        /// </summary>
        public IList<KeyValuePair<ParameterParser, RouteNode>> ParsedChildren { get; }

        /// <summary>
        /// Returns true if this node is read-only.
        /// </summary>
        public bool Frozen { get; }

        /// <summary>
        /// Gets the only branch reachable from this node when it has no handlers and a single child branch kind.
        /// </summary>
        public RouteSingleBranch SingleBranch { get; }

        public RouteNode()
        {
            HandlerRegistrations = new Dictionary<HttpVerb, IList<HandlerRegistration>>();
            LiteralChildren = new Dictionary<ReadOnlyMemory<char>, RouteNode>(ReadOnlyMemoryCharComparer.Instance);
            ParsedChildren = new List<KeyValuePair<ParameterParser, RouteNode>>();
        }

        private RouteNode(RouteNode src, bool freeze)
        {
            if (!freeze)
            {
                CopyCollection
                (
                    src.ParsedChildren,
                    ParsedChildren = new List<KeyValuePair<ParameterParser, RouteNode>>(src.ParsedChildren.Count),
                    static kvp => new KeyValuePair<ParameterParser, RouteNode>(kvp.Key, kvp.Value.Copy(freeze: false))
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
                    .Select
                    (
                        static kvp => new KeyValuePair<ParameterParser, RouteNode>(kvp.Key, kvp.Value.Copy(freeze: true))
                    )
                    .ToImmutableArray();

                HandlerRegistrations = src
                    .HandlerRegistrations
                    .ToFrozenDictionary
                    (
                        static kvp => kvp.Key,
                        static kvp => (IList<HandlerRegistration>) kvp.Value.ToImmutableArray()
                    );

                if (HandlerRegistrations.Count is 0)
                    SingleBranch = (LiteralChildren.Count, ParsedChildren.Count) switch
                    {
                        (1, 0) => LiteralChildren.Single(),
                        (0, 1) => ParsedChildren.Single(),
                        _ => default
                    };

                Frozen = true;
            }
        }

        /// <summary>
        /// Creates a deep-copy from this node.
        /// </summary>
        public RouteNode Copy(bool freeze) => new(this, freeze);
    }
}
