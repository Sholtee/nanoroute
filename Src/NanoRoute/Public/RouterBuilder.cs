/********************************************************************************
* RouterBuilder.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace NanoRoute
{
    using Internals;

    /// <summary>
    /// Builds a concrete router type together with its strongly typed configuration object.
    /// </summary>
    /// <typeparam name="TRouter">The router type produced by <see cref="CreateRouter(Action{TConfig})"/>.</typeparam>
    /// <typeparam name="TConfig">The configuration type exposed by <see cref="RouterConfig"/>.</typeparam>
    /// <example>
    /// <code>
    /// MyRouter router = MyRouter
    ///     .CreateBuilder()
    ///     .AddDefaultValueParsers()
    ///     .AddHandler("GET", "/health/", (context, _) =&gt; Results.Ok())
    ///     .CreateRouter();
    /// </code>
    /// </example>
    public sealed class RouterBuilder<TRouter, TConfig> : RouteScopeBuilder where TRouter : RouterBase<TConfig> where TConfig : RouterConfig, new()
    {
        private readonly RouterFactoryDelegate<TRouter, TConfig> _routerFactory;

        /// <summary>
        /// Creates a builder that can produce <typeparamref name="TRouter"/> instances.
        /// </summary>
        /// <param name="routerFactory">
        /// A factory that receives this builder and returns a router backed by its current route snapshot.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="routerFactory"/> is <see langword="null"/>.</exception>
        public RouterBuilder(RouterFactoryDelegate<TRouter, TConfig> routerFactory) : base()
        {
            Ensure.NotNull(routerFactory);

            _routerFactory = routerFactory;
        }

        /// <summary>
        /// Registers a parser that can convert a route segment into a typed value and bind parser arguments once during route registration.
        /// </summary>
        /// <param name="parserName">The name used in route patterns such as <c>{id:int(min=1)}</c>.</param>
        /// <param name="bindArguments">Converts raw parser arguments into typed values once per route-template branch.</param>
        /// <param name="tryParseDelegate">The delegate that validates and parses a single path segment.</param>
        /// <returns>The current <see cref="RouterBuilder{TRouter, TConfig}"/> instance.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="parserName"/>, <paramref name="bindArguments"/>, or
        /// <paramref name="tryParseDelegate"/> is <see langword="null"/>.
        /// </exception>
        /// <example>
        /// <code>
        /// builder.AddValueParser("slug", static rawArgs =&gt; null, static context =&gt;
        ///     ValueTask.FromResult(new ValueParseResult(context.Segment.Length &gt; 0, context.Segment.ToString())));
        /// </code>
        /// </example>
        public new RouterBuilder<TRouter, TConfig> AddValueParser(string parserName, BindArgumentsDelegate bindArguments, ValueParserDelegate tryParseDelegate)
        {
            base.AddValueParser(parserName, bindArguments, tryParseDelegate);
            return this;
        }

        /// <summary>
        /// Registers a handler for a single HTTP method.
        /// </summary>
        /// <param name="verb">The HTTP method that activates the handler.</param>
        /// <param name="pattern">
        /// The route pattern to match. Literal segments are matched case-insensitively, parameter segments use
        /// registered parsers in the form <c>{parameterName:parserName}</c>. Exact patterns must end with
        /// <c>/</c>, and prefix patterns must end with <c>/*</c>.
        /// </param>
        /// <param name="handler">
        /// The handler to execute. If several handlers match, calling the supplied <c>next</c> delegate continues
        /// the pipeline with the next compatible handler from the already selected route branch.
        /// </param>
        /// <returns>The current router instance.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="verb"/>, <paramref name="pattern"/>, or <paramref name="handler"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not a supported HTTP method.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> has invalid route-template syntax.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="pattern"/> uses an unsupported optional parameter or list parser, references
        /// a value parser that has not been registered yet, or reuses a parser-backed branch with a different
        /// parameter name.
        /// </exception>
        /// <example>
        /// <code>
        /// builder.AddHandler("GET", "/files/{path:any}/*", (context, next) =&gt;
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
        /// Creates a router from the builder's current routes, parser registrations, and configuration.
        /// </summary>
        /// <returns>A new <typeparamref name="TRouter"/> instance.</returns>
        /// <remarks>
        /// The created router is an immutable snapshot. Later changes to the builder or its configuration do not
        /// affect routers that have already been created.
        /// </remarks>
        /// <example>
        /// <code>
        /// MyRouter router = builder.CreateRouter();
        /// </code>
        /// </example>
        public TRouter CreateRouter() => CreateRouter(static _ => { });

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="configureRouting"></param>
        /// <returns></returns>
        public TRouter CreateRouter(Action<TConfig> configureRouting)
        {
            Ensure.NotNull(configureRouting);

            TConfig config = new();
            configureRouting(config);

            return _routerFactory(this, config);
        }
    }
}
