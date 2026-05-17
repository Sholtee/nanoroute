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
    /// extensions register middleware against that captured endpoint. Extension authors can use
    /// <see cref="Prefix"/> when they need access to the endpoint's scoped route builder.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.AddEndPoint("POST", "/users/", endpoint =&gt; endpoint
    ///     .WithJsonBody&lt;CreateUserRequest&gt;("body")
    ///     .WithHandler((context, _) =&gt; CreateUser(context)));
    /// </code>
    /// </example>
    public sealed class EndPointBuilder
    {
        private readonly string _matchKind;

        private readonly IReadOnlyCollection<string> _verbs;

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

            Prefix = scope.CreatePrefix(pattern);

            _verbs = [.. verbs];
        }

        /// <summary>
        /// Adds a handler delegate to the endpoint.
        /// </summary>
        /// <param name="handler">
        /// The handler to run when the endpoint matches. If the handler calls the supplied <c>next</c> delegate,
        /// routing continues with the next compatible handler registered for the same endpoint branch.
        /// </param>
        /// <returns>The current <see cref="EndPointBuilder"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when the endpoint's captured HTTP method is not supported.</exception>
        /// <example>
        /// <code>
        /// endpoint.WithHandler((context, _) =&gt; Results.Ok(context.Parameters));
        /// </code>
        /// </example>
        public EndPointBuilder WithHandler(RequestHandlerDelegate handler)
        {
            Prefix.AddHandler(_verbs, _matchKind, handler);

            return this;
        }

        /// <summary>
        /// Gets the route scope that backs the endpoint.
        /// </summary>
        /// <remarks>
        /// Endpoint-aware extensions can use this scope to access endpoint-local value parsers, metadata, and
        /// lower-level handler registration APIs.
        /// </remarks>
        public RouteScopeBuilder Prefix { get; }
    }

    /// <summary>
    /// Adds endpoint-building helpers to route scope builders.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.AddEndPoint("GET", "/users/{id:int}/", endpoint =&gt; endpoint
    ///     .WithHandler((context, _) =&gt; Results.Ok(context.Parameters["id"])));
    /// </code>
    /// </example>
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
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, or
            /// <paramref name="pattern"/> is <see langword="null"/>.
            /// </exception>
            /// <exception cref="ArgumentException">
            /// Thrown when <paramref name="pattern"/> is not an exact or prefix route pattern, has invalid
            /// route-template syntax, or an entry in <paramref name="verbs"/> is not a supported HTTP method.
            /// </exception>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a
            /// missing value parser, or conflicts with an existing parser-backed branch.
            /// </exception>
            /// <example>
            /// <code>
            /// EndPointBuilder endpoint = builder.CreateEndPoint(["GET", "HEAD"], "/users/{id:int}/");
            /// </code>
            /// </example>
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
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verb"/>, or
            /// <paramref name="pattern"/> is <see langword="null"/>.
            /// </exception>
            /// <exception cref="ArgumentException">
            /// Thrown when <paramref name="pattern"/> is not an exact or prefix route pattern, has invalid
            /// route-template syntax, or <paramref name="verb"/> is not a supported HTTP method.
            /// </exception>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a
            /// missing value parser, or conflicts with an existing parser-backed branch.
            /// </exception>
            /// <example>
            /// <code>
            /// EndPointBuilder endpoint = builder.CreateEndPoint("GET", "/users/{id:int}/");
            /// </code>
            /// </example>
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
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>,
            /// <paramref name="pattern"/>, or <paramref name="configureEndPoint"/> is <see langword="null"/>.
            /// </exception>
            /// <exception cref="ArgumentException">
            /// Thrown when <paramref name="pattern"/> is not an exact or prefix route pattern, has invalid
            /// route-template syntax, or an entry in <paramref name="verbs"/> is not a supported HTTP method.
            /// </exception>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a
            /// missing value parser, or conflicts with an existing parser-backed branch.
            /// </exception>
            /// <example>
            /// <code>
            /// builder.AddEndPoint(["POST", "PUT"], "/users/{id:int}/", endpoint =&gt; endpoint
            ///     .WithJsonBody&lt;UpdateUserRequest&gt;("body")
            ///     .WithHandler((context, _) =&gt; SaveUser(context)));
            /// </code>
            /// </example>
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
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verb"/>,
            /// <paramref name="pattern"/>, or <paramref name="configureEndPoint"/> is <see langword="null"/>.
            /// </exception>
            /// <exception cref="ArgumentException">
            /// Thrown when <paramref name="pattern"/> is not an exact or prefix route pattern, has invalid
            /// route-template syntax, or <paramref name="verb"/> is not a supported HTTP method.
            /// </exception>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a
            /// missing value parser, or conflicts with an existing parser-backed branch.
            /// </exception>
            /// <example>
            /// <code>
            /// builder.AddEndPoint("GET", "/users/{id:int}/", endpoint =&gt; endpoint
            ///     .WithHandler((context, _) =&gt; Results.Ok(context.Parameters["id"])));
            /// </code>
            /// </example>
            public TBuilder AddEndPoint(string verb, string pattern, Action<EndPointBuilder> configureEndPoint)
            {
                Ensure.NotNull(verb);

                return routeScopeBuilder.AddEndPoint([verb], pattern, configureEndPoint);
            }
        }
    }
}

