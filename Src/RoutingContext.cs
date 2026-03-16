/********************************************************************************
* RoutingContext.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;

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
        internal enum HttpVerb
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
        internal sealed record ParameterParser(string Name, ParameterParserDelegate TryParse)
        {
            /// <summary>
            /// Gets the request-context parameter name that receives the parsed value.
            /// </summary>
            public string? ParameterName { get; init; }
        }

        /// <summary>
        /// Represents a request <paramref cref="Handler"/> registration attached to a particular <see cref="Pattern"/>.
        /// </summary>
        internal sealed record HandlerRegistration(RequestHandler<TRequest, TResponse> Handler, string Pattern)
        {
            /// <summary>
            /// Gets the parameter snapshot associated with the current match.
            /// </summary>
            public Dictionary<string, object?>? AttachedParameters { get; init; }
         }

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
            /// Gets or sets the parser used by this node when it represents a parameter segment.
            /// </summary>
            public ParameterParser? ParameterParser { get; init; }

            /// <summary>
            /// Gets or sets the segment for which this node is created
            /// </summary>
            public required string Segment { get; init; }

            /// <summary>
            /// Gets literal child nodes keyed by case-insensitive segment value.
            /// </summary>
            public Dictionary<string, RouteNode> ExactChildren { get; } = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Gets parameter-based child nodes evaluated after literal matches.
            /// </summary>
            public List<RouteNode> ParameterizedChildren { get; } = [];

            public RouteNode Copy()  // TODO: test this carefully 
            {
                RouteNode result = new()
                {
                    ParameterParser = ParameterParser,
                    Segment = Segment
                };

                result.ParameterizedChildren.AddRange
                (
                    ParameterizedChildren.Select(static n => n.Copy())
                );

                CopyDict(HandlerRegistrations, result.HandlerRegistrations, static v => [..v]);
                CopyDict(ExactChildren, result.ExactChildren, static n => n.Copy());

                return result;

                static void CopyDict<TKey, TValue>(Dictionary<TKey, TValue> src, Dictionary<TKey, TValue> dst, Func<TValue, TValue> valueCopy)
                {
                    foreach(KeyValuePair<TKey, TValue> kvp in src)
                        dst.Add(kvp.Key, valueCopy(kvp.Value));
                }
            }
        }

        internal RouteNode Root { get; init; } = null!;  // this cannot be required =(
        #endregion
    }
}