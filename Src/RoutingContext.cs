/********************************************************************************
* RoutingContext.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace NanoRoute
{
    /// <summary>
    /// TOTO
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    public abstract class RoutingContext<TRequest, TResponse>
    {
        #region Protected
        private protected enum HttpVerb
        {
            Get,
            Post,
            Put,
            Delete,
            Patch,
            Head,
            Options,
            Trace
        }

        /// <summary>
        /// Stores a named route-segment parser and its optional bound parameter name.
        /// </summary>
        private protected sealed record ParameterParser(string Name, ParameterParserDelegate TryParse)
        {
            /// <summary>
            /// Gets the request-context parameter name that receives the parsed value.
            /// </summary>
            public string? ParameterName { get; init; }
        }

        /// <summary>
        /// Represents a handler attached to a matched route node.
        /// </summary>
        private protected sealed record HandlerRegistration(RequestHandler<TRequest, TResponse> Handler, bool Prefix, RouteNode Node)
        {
            /// <summary>
            /// Gets the parameter snapshot associated with the current match.
            /// </summary>
            public Dictionary<string, object?>? AttachedParameters { get; init; }
        }

        /// <summary>
        /// Represents a node in the per-verb route tree.
        /// </summary>
        private protected sealed class RouteNode
        {
            /// <summary>
            /// Gets the handlers registered for the current route node.
            /// </summary>
            public Dictionary<HttpVerb, List<HandlerRegistration>> HandlerRegistrations { get; } = [];

            /// <summary>
            /// Gets or sets the parser used by this node when it represents a parameter segment.
            /// </summary>
            public ParameterParser? ParameterParser { get; set; }

            /// <summary>
            /// Gets or sets the original route pattern registered for this node.
            /// </summary>
            public string? Pattern { get; set; }

            /// <summary>
            /// Gets literal child nodes keyed by case-insensitive segment value.
            /// </summary>
            public Dictionary<string, RouteNode> ExactChildren { get; } = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Gets parameter-based child nodes evaluated after literal matches.
            /// </summary>
            public List<RouteNode> ParameterizedChildren { get; } = [];
        }

        private protected readonly RouteNode _root;

        /// <summary>
        /// Creates a new <see cref="RoutingContext{TRequest, TResponse}"/> class
        /// </summary>
        /// <param name="root"></param>
        private protected RoutingContext(RouteNode root) => _root = root;
        #endregion
    }
}