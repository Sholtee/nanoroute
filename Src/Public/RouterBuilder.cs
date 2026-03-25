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
    /// 
    /// </summary>
    /// <typeparam name="TRouter"></typeparam>
    /// <typeparam name="TConfig"></typeparam>
    public sealed class RouterBuilder<TRouter, TConfig> : RouteBuilder where TRouter : Router where TConfig: RouterConfig, new()
    {
        private readonly Func<RouterBuilder<TRouter, TConfig>, TRouter> _routerFactory;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="routerFactory"></param>
        public RouterBuilder(Func<RouterBuilder<TRouter, TConfig>, TRouter> routerFactory): base()
        {
            Ensure.NotNull(routerFactory);

            _routerFactory = routerFactory;
        }

        /// <summary>
        /// Registers a parser that can convert a route segment into a typed parameter value.
        /// </summary>
        /// <param name="parserName">The name used in route patterns such as <c>{id:int}</c>.</param>
        /// <param name="tryParseDelegate">The delegate that validates and parses a single path segment.</param>
        /// <returns>The current <see cref="RouterBuilder{TRouter, TConfig}"/> instance.</returns>
        /// <remarks>
        /// If a parser is already registered under the same <paramref name="parserName"/>, the new registration
        /// replaces the existing one.
        /// </remarks>
        public new RouterBuilder<TRouter, TConfig> AddParameterParser(string parserName, ParameterParserDelegate tryParseDelegate)
        {
            base.AddParameterParser(parserName, tryParseDelegate);
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
        /// <returns>The current router instance.</returns>
        /// <example>
        /// <code>
        /// router.AddHandler("/health", (context, next) =&gt; Results.Ok());
        /// </code>
        /// </example>
        public new RouterBuilder<TRouter, TConfig> AddHandler(string pattern, RequestHandler handler)
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
        /// <returns>The current router instance.</returns>
        /// <example>
        /// <code>
        /// router.AddHandler(
        ///     ["GET", "POST"],
        ///     "/api/items/{id:int}",
        ///     (context, next) =&gt; Results.Ok(context.Parameters["id"]));
        /// </code>
        /// </example>
        public new RouterBuilder<TRouter, TConfig> AddHandler(IEnumerable<string> verbs, string pattern, RequestHandler handler)
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
        /// <exception cref="InvalidOperationException">
        /// Thrown when the pattern references a parameter parser that has not been registered yet.
        /// </exception>
        /// <example>
        /// <code>
        /// router.AddHandler("GET", "/files/{path:any}/", (context, next) =&gt;
        /// {
        ///     string path = (string) context.Parameters["path"]!;
        ///     return ServeFile(path);
        /// });
        /// </code>
        /// </example>
        public new RouterBuilder<TRouter, TConfig> AddHandler(string verb, string pattern, RequestHandler handler)
        {
            base.AddHandler(verb, pattern, handler);
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="configureRoutes"></param>
        /// <returns></returns>
        public new RouterBuilder<TRouter, TConfig> WithBase(string pattern, Action<RouteBuilder> configureRoutes)
        {
            base.WithBase(pattern, configureRoutes);
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateConfig"></param>
        /// <returns></returns>
        public RouterBuilder<TRouter, TConfig> WithConfiguration(Action<TConfig> updateConfig)
        {
            Ensure.NotNull(updateConfig);

            updateConfig(RouterConfig);

            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        public TConfig RouterConfig { get; } = new();

        /// <summary>
        /// TODO
        /// </summary>
        /// <returns></returns>
        public TRouter CreateRouter() => _routerFactory(this);
    }
}
