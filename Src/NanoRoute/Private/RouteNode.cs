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
    /// Represents a node in the route tree.
    /// </summary>
    internal sealed class RouteNode
    {
        /// <summary>
        /// Gets the verb-tagged handlers registered for the current route node.
        /// </summary>
        public IList<KeyValuePair<HttpVerb, HandlerRegistration>> Handlers { get; }

        /// <summary>
        /// Gets literal branches keyed by case-insensitive segment value.
        /// </summary>
        public IDictionary<ReadOnlyMemory<char>, RouteNode> LiteralBranches { get; }

        /// <summary>
        /// Gets parser-based branches.
        /// </summary>
        public IList<KeyValuePair<ParameterParser, RouteNode>> ParsedBranches { get; }

        /// <summary>
        /// Returns true if this node is read-only.
        /// </summary>
        public bool Frozen { get; }

        /// <summary>
        /// Gets the only branch reachable from this node when it has no handlers and a single child branch kind.
        /// </summary>
        public object? SingleBranch { get; }

        public RouteNode()
        {
            Handlers = new List<KeyValuePair<HttpVerb, HandlerRegistration>>();
            LiteralBranches = new Dictionary<ReadOnlyMemory<char>, RouteNode>(ReadOnlyMemoryCharComparer.Instance);
            ParsedBranches = new List<KeyValuePair<ParameterParser, RouteNode>>();
        }

        private RouteNode(RouteNode src)
        {
            LiteralBranches = src.LiteralBranches.ToFrozenDictionary
            (
                static kvp => kvp.Key,
                static kvp => kvp.Value.Freeze(),
                ReadOnlyMemoryCharComparer.Instance
            );

            ParsedBranches = src
                .ParsedBranches
                .Select
                (
                    static kvp => new KeyValuePair<ParameterParser, RouteNode>
                    (
                        kvp.Key,
                        kvp.Value.Freeze()
                    )
                )
                .ToImmutableArray();

            Handlers = src.Handlers.ToImmutableArray();

            if (Handlers.Count is 0)
                SingleBranch = (LiteralBranches.Count, ParsedBranches.Count) switch
                {
                    (1, 0) => LiteralBranches.Single(),
                    (0, 1) => ParsedBranches.Single(),
                    _ => default
                };

            Frozen = true;
        }

        /// <summary>
        /// Creates a frozen snapshot from this node.
        /// </summary>
        public RouteNode Freeze() => new(this);
    }
}
