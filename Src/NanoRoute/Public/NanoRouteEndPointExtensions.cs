/********************************************************************************
* NanoRouteEndPointExtensions.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// Builds handlers and middleware that belong to a single route endpoint.
    /// </summary>
    /// <remarks>
    /// Endpoint builders capture the endpoint route pattern and HTTP verbs once, then let endpoint-aware
    /// extensions register middleware against that captured endpoint. The underlying route scope remains private;
    /// extension authors can use <see cref="Metadata"/> for endpoint-scoped build-time settings.
    /// </remarks>
    public sealed class EndPointBuilder
    {
        private readonly string _matchKind;

        private readonly IReadOnlyCollection<string> _verbs;

        private readonly RouteScopeBuilder _prefix;

        internal EndPointBuilder(RouteScopeBuilder scope, IEnumerable<string> verbs, string pattern)
        {
            Ensure.NotNull(scope);
            Ensure.NotNull(pattern);
            Ensure.NotNull(verbs);

            switch (pattern)
            {
                case string _ when pattern.EndsWith(RouteScopeBuilder.CurrentExact):
                    pattern += "*";
                    _matchKind = RouteScopeBuilder.CurrentExact;
                    break;

                case string _ when pattern.EndsWith(RouteScopeBuilder.CurrentPrefix):
                    _matchKind = RouteScopeBuilder.CurrentPrefix;
                    break;

                default:
                    throw new ArgumentException(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, pattern.Length > 0 ? pattern.Length - 1 : "-"), nameof(pattern));
            }

            _prefix = scope.CreatePrefix(pattern);

            _verbs = [.. verbs];
        }

        /// <summary>
        /// Adds a handler or middleware delegate to the endpoint.
        /// </summary>
        /// <param name="handler">
        /// The handler to run when the endpoint matches. If the handler calls the supplied <c>next</c> delegate,
        /// routing continues with the next compatible handler registered for the same endpoint branch.
        /// </param>
        /// <returns>The current <see cref="EndPointBuilder"/> instance.</returns>
        public EndPointBuilder WithHandler(RequestHandlerDelegate handler)
        {
            Ensure.NotNull(handler);

            _prefix.AddHandler(_verbs, _matchKind, handler);

            return this;
        }

        /// <summary>
        /// Gets the endpoint-scoped metadata visible to endpoint-aware builder extensions.
        /// </summary>
        /// <remarks>
        /// The metadata is copied from the parent route scope when the endpoint is created. Later updates made
        /// through this property stay local to this endpoint scope.
        /// </remarks>
        public BuilderMetadata Metadata => _prefix.Metadata;
    }

    /// <summary>
    /// Adds endpoint-building helpers to route scope builders.
    /// </summary>
    public static class NanoRouteEndPointExtensions
    {
        extension<TBuilder>(TBuilder routeScopeBuilder) where TBuilder : RouteScopeBuilder
        {
            /// <summary>
            /// Creates an endpoint builder for the selected HTTP methods and route pattern.
            /// </summary>
            /// <param name="verbs">The HTTP methods handled by the endpoint.</param>
            /// <param name="pattern">
            /// The endpoint route pattern. Exact patterns must end with <c>/</c>, and prefix patterns must end
            /// with <c>/*</c>.
            /// </param>
            /// <returns>An endpoint builder rooted at <paramref name="pattern"/>.</returns>
            public EndPointBuilder CreateEndPoint(IEnumerable<string> verbs, string pattern)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(verbs);
                Ensure.NotNull(pattern);

                return new EndPointBuilder(routeScopeBuilder, verbs, pattern);
            }

            /// <summary>
            /// Creates an endpoint builder for a single HTTP method and route pattern.
            /// </summary>
            /// <param name="verb">The HTTP method handled by the endpoint.</param>
            /// <param name="pattern">
            /// The endpoint route pattern. Exact patterns must end with <c>/</c>, and prefix patterns must end
            /// with <c>/*</c>.
            /// </param>
            /// <returns>An endpoint builder rooted at <paramref name="pattern"/>.</returns>
            public EndPointBuilder CreateEndPoint(string verb, string pattern)
            {
                Ensure.NotNull(verb);

                return routeScopeBuilder.CreateEndPoint([verb], pattern);
            }

            /// <summary>
            /// Configures an endpoint for the selected HTTP methods and returns the current route scope builder.
            /// </summary>
            /// <param name="verbs">The HTTP methods handled by the endpoint.</param>
            /// <param name="pattern">
            /// The endpoint route pattern. Exact patterns must end with <c>/</c>, and prefix patterns must end
            /// with <c>/*</c>.
            /// </param>
            /// <param name="configureEndPoint">A callback that registers endpoint-local handlers and middleware.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            public TBuilder AddEndPoint(IEnumerable<string> verbs, string pattern, Action<EndPointBuilder> configureEndPoint)
            {
                Ensure.NotNull(configureEndPoint);

                configureEndPoint
                (
                    routeScopeBuilder.CreateEndPoint(verbs, pattern)
                );

                return routeScopeBuilder;
            }

            /// <summary>
            /// Configures an endpoint for a single HTTP method and returns the current route scope builder.
            /// </summary>
            /// <param name="verb">The HTTP method handled by the endpoint.</param>
            /// <param name="pattern">
            /// The endpoint route pattern. Exact patterns must end with <c>/</c>, and prefix patterns must end
            /// with <c>/*</c>.
            /// </param>
            /// <param name="configureEndPoint">A callback that registers endpoint-local handlers and middleware.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            public TBuilder AddEndPoint(string verb, string pattern, Action<EndPointBuilder> configureEndPoint)
            {
                Ensure.NotNull(verb);

                return routeScopeBuilder.AddEndPoint([verb], pattern, configureEndPoint);
            }
        }
    }
}

