/********************************************************************************
* RouterBuilder.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace NanoRoute
{
    using Internals;

    /// <summary>
    /// Builds a concrete <see cref="Router"/> type together with its strongly typed configuration object.
    /// </summary>
    /// <typeparam name="TRouter">The router type produced by <see cref="CreateRouter"/>.</typeparam>
    /// <typeparam name="TConfig">The configuration type exposed by <see cref="RouterConfig"/>.</typeparam>
    public sealed class RouterBuilder<TRouter, TConfig> : RouteBuilder where TRouter : Router where TConfig: RouterConfig, new()
    {
        private readonly Func<RouterBuilder<TRouter, TConfig>, TRouter> _routerFactory;

        /// <summary>
        /// Creates a builder that can produce <typeparamref name="TRouter"/> instances.
        /// </summary>
        /// <param name="routerFactory">
        /// A factory that receives this builder and returns a router backed by its current route snapshot.
        /// </param>
        public RouterBuilder(Func<RouterBuilder<TRouter, TConfig>, TRouter> routerFactory): base()
        {
            Ensure.NotNull(routerFactory);

            _routerFactory = routerFactory;
        }

        /// <summary>
        /// Registers a parser that can convert a route segment into a typed value.
        /// </summary>
        /// <param name="parserName">The name used in route patterns such as <c>{id:int}</c>.</param>
        /// <param name="tryParseDelegate">The delegate that validates and parses a single path segment.</param>
        /// <returns>The current <see cref="RouterBuilder{TRouter, TConfig}"/> instance.</returns>
        /// <remarks>
        /// If a parser is already registered under the same <paramref name="parserName"/>, the new registration
        /// replaces the existing one.
        /// </remarks>
        public new RouterBuilder<TRouter, TConfig> AddSegmentParser(string parserName, SegmentParserDelegate tryParseDelegate)
        {
            base.AddSegmentParser(parserName, tryParseDelegate);
            return this;
        }

        /// <summary>
        /// Registers a parser that can convert a route segment into a typed value and bind parser arguments once during route registration.
        /// </summary>
        /// <param name="parserName">The name used in route patterns such as <c>{id:int(min=1)}</c>.</param>
        /// <param name="bindArguments">Converts raw parser arguments into typed values once per route-template branch.</param>
        /// <param name="tryParseDelegate">The delegate that validates and parses a single path segment.</param>
        /// <returns>The current <see cref="RouterBuilder{TRouter, TConfig}"/> instance.</returns>
        public new RouterBuilder<TRouter, TConfig> AddSegmentParser(string parserName, Func<IReadOnlyDictionary<string, string>, object?> bindArguments, SegmentParserDelegate tryParseDelegate)
        {
            base.AddSegmentParser(parserName, bindArguments, tryParseDelegate);
            return this;
        }

        /// <summary>
        /// Registers a handler for all supported HTTP methods.
        /// </summary>
        /// <param name="pattern">
        /// The route pattern to match. Literal segments are matched case-insensitively, parameter segments use
        /// registered parsers in the form <c>{parameterName:parserName}</c>, and a trailing <c>/</c> turns the
        /// pattern into a prefix match. Without a trailing slash, the pattern matches only the exact path.
        /// </param>
        /// <param name="handler">The handler to execute when the pattern matches.</param>
        /// <returns>The current <see cref="RouterBuilder{TRouter, TConfig}"/> instance.</returns>
        /// <example>
        /// <code>
        /// builder.AddHandler("/health", (context, next) =&gt; Results.Ok());
        /// </code>
        /// </example>
        public new RouterBuilder<TRouter, TConfig> AddHandler(string pattern, RequestHandlerDelegate handler)
        {
            base.AddHandler(pattern, handler);
            return this;
        }

        /// <summary>
        /// Registers the same handler for multiple HTTP methods.
        /// </summary>
        /// <param name="verbs">The HTTP methods that should use the handler.</param>
        /// <param name="pattern">
        /// The route pattern to match. Literal segments are matched case-insensitively, parameter segments use
        /// registered parsers in the form <c>{parameterName:parserName}</c>, and a trailing <c>/</c> turns the
        /// pattern into a prefix match. Without a trailing slash, the pattern matches only the exact path.
        /// </param>
        /// <param name="handler">The handler to execute when the route matches.</param>
        /// <returns>The current <see cref="RouterBuilder{TRouter, TConfig}"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when some of the <paramref name="verbs"/> represent a not supported HTTP method.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the <paramref name="pattern"/> references a segment parser that has not been registered yet.</exception>
        /// <example>
        /// <code>
        /// builder.AddHandler(
        ///     ["GET", "POST"],
        ///     "/api/items/{id:int}",
        ///     (context, next) =&gt; Results.Ok(context.Parameters["id"]));
        /// </code>
        /// </example>
        public new RouterBuilder<TRouter, TConfig> AddHandler(IEnumerable<string> verbs, string pattern, RequestHandlerDelegate handler)
        {
            base.AddHandler(verbs, pattern, handler);
            return this;
        }

        /// <summary>
        /// Registers a handler for a single HTTP method.
        /// </summary>
        /// <param name="verb">The HTTP method that activates the handler.</param>
        /// <param name="pattern">
        /// The route pattern to match. Literal segments are matched case-insensitively, parameter segments use
        /// registered parsers in the form <c>{parameterName:parserName}</c>, and a trailing <c>/</c> turns the
        /// pattern into a prefix match. Without a trailing slash, the pattern matches only the exact path.
        /// </param>
        /// <param name="handler">
        /// The handler to execute. If several handlers match, calling the supplied <c>next</c> delegate continues
        /// the pipeline with the next compatible handler.
        /// </param>
        /// <returns>The current router instance.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not a supported HTTP method.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the <paramref name="pattern"/> references a segment parser that has not been registered yet.</exception>
        /// <example>
        /// <code>
        /// builder.AddHandler("GET", "/files/{path:any}/", (context, next) =&gt;
        /// {
        ///     string path = (string) context.Parameters["path"]!;
        ///     return ServeFile(path);
        /// });
        /// </code>
        /// </example>
        public new RouterBuilder<TRouter, TConfig> AddHandler(string verb, string pattern, RequestHandlerDelegate handler)
        {
            base.AddHandler(verb, pattern, handler);
            return this;
        }

        /// <summary>
        /// Creates a scoped child builder under the given base prefix, invokes a configuration callback, and returns this builder.
        /// </summary>
        /// <param name="pattern">The base prefix that child routes will be registered under.</param>
        /// <param name="configureRoutes">A callback that configures routes on the child builder.</param>
        /// <returns>The current builder.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> is not a valid route <paramref name="pattern"/> or does not end with <c>/</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the <paramref name="pattern"/> references a segment parser that has not been registered yet.</exception>
        public new RouterBuilder<TRouter, TConfig> WithBase(string pattern, Action<RouteBuilder> configureRoutes)
        {
            base.WithBase(pattern, configureRoutes);
            return this;
        }

        /// <summary>
        /// Updates the router configuration object that will be used by future router instances.
        /// </summary>
        /// <param name="updateConfig">A callback that mutates <see cref="RouterConfig"/>.</param>
        /// <returns>The current builder.</returns>
        public RouterBuilder<TRouter, TConfig> WithConfiguration(Action<TConfig> updateConfig)
        {
            Ensure.NotNull(updateConfig);

            updateConfig(RouterConfig);

            return this;
        }

        /// <summary>
        /// Gets the mutable configuration object applied when <see cref="CreateRouter"/> is called.
        /// </summary>
        public TConfig RouterConfig { get; } = new();

        /// <summary>
        /// Creates a router from the builder's current routes, parser registrations, and configuration.
        /// </summary>
        /// <returns>A new <typeparamref name="TRouter"/> instance.</returns>
        /// <remarks>
        /// The created router is an immutable snapshot. Later changes to the builder or its configuration do not
        /// affect routers that have already been created.
        /// </remarks>
        public TRouter CreateRouter() => _routerFactory(this);
    }
}
