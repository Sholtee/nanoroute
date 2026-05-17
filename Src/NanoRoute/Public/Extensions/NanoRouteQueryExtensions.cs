/********************************************************************************
* NanoRouteQueryExtensions.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Net.Http;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// Defines how query-binding middleware handles query-string parameters that are not declared in the binding descriptor.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.ConfigureQueryParsing(config =&gt; config with
    /// {
    ///     UnexpectedParameterBehavior = UnexpectedParameterBehavior.Reject
    /// });
    /// </code>
    /// </example>
    public enum UnexpectedParameterBehavior
    {
        /// <summary>
        /// Ignore undeclared query-string parameters.
        /// </summary>
        /// <example>
        /// <code>
        /// builder.ConfigureQueryParsing(config =&gt; config with
        /// {
        ///     UnexpectedParameterBehavior = UnexpectedParameterBehavior.Ignore
        /// });
        /// </code>
        /// </example>
        Ignore,

        /// <summary>
        /// Reject undeclared query-string parameters with a <c>400 Bad Request</c> error.
        /// </summary>
        /// <example>
        /// <code>
        /// builder.ConfigureQueryParsing(config =&gt; config with
        /// {
        ///     UnexpectedParameterBehavior = UnexpectedParameterBehavior.Reject
        /// });
        /// </code>
        /// </example>
        Reject,

        // Dangerous since it lets the caller override parameter values provided by earlier middlewares

        //AcceptAsString
    }

    /// <summary>
    /// Configures how query-binding middleware parses query strings.
    /// </summary>
    /// <remarks>
    /// Query-binding middleware snapshots the configuration that is current when it is registered.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.ConfigureQueryParsing(config =&gt; config with
    /// {
    ///     UnexpectedParameterBehavior = UnexpectedParameterBehavior.Reject
    /// });
    /// </code>
    /// </example>
    public sealed record QueryParsingConfig
    {
        /// <summary>
        /// Gets how undeclared query-string parameters are handled.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="UnexpectedParameterBehavior.Ignore"/>, so additional query-string keys that are
        /// not present in the binding descriptor do not affect request processing.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the assigned value is not a defined <see cref="UnexpectedParameterBehavior"/> value.</exception>
        /// <example>
        /// <code>
        /// builder.ConfigureQueryParsing(config =&gt; config with
        /// {
        ///     UnexpectedParameterBehavior = UnexpectedParameterBehavior.Reject
        /// });
        /// </code>
        /// </example>
        public UnexpectedParameterBehavior UnexpectedParameterBehavior
        {
            get;
            init
            {
                if (!Enum.IsDefined(typeof(UnexpectedParameterBehavior), value))
                    throw new ArgumentOutOfRangeException(nameof(value));
                field = value;
            }
        } = UnexpectedParameterBehavior.Ignore;

        /// <summary>
        /// Gets the default query-parsing configuration.
        /// </summary>
        /// <example>
        /// <code>
        /// QueryParsingConfig config = QueryParsingConfig.Default;
        /// </code>
        /// </example>
        public static QueryParsingConfig Default { get; } = new();
    }

    /// <summary>
    /// Adds query-parameter binding helpers to NanoRoute.
    /// </summary>
    /// <example>
    /// <code>
    /// builder
    ///     .AddDefaultValueParsers()
    ///     .AddQueryBindings("{page?:int(min=1)}")
    ///     .AddHandler("GET", "/items/", (context, _) =&gt; Results.Ok(context.Parameters));
    /// </code>
    /// </example>
    public static class NanoRouteQueryExtensions
    {
        #region Private
        private static RequestHandlerDelegate CreateHandler(RouteScopeBuilder routeScopeBuilder, string bindings)
        {
            Ensure.NotNull(bindings);

            Dictionary<ReadOnlyMemory<char>, ParameterParser> parsedBindingsTmp = new(ReadOnlyMemoryCharComparer.Instance);

            foreach (ParameterDefinition parameterDefinition in DslParser.ParseQueryPattern(bindings))
            {
                if (!routeScopeBuilder.ValueParsers.TryGetValue(parameterDefinition.ValueParser.Name, out ValueParserRegistration? parserRegistration))
                    throw new InvalidOperationException
                    (
                        string.Format(Resources.Culture, Resources.ERR_NO_SUCH_PARSER, parameterDefinition.ValueParser.Name)
                    );

                parsedBindingsTmp.Add
                (
                    parameterDefinition.ParameterName!.AsMemory(),
                    new ParameterParser
                    (
                        parameterDefinition,
                        parserRegistration.Parse,
                        parserRegistration.BindArguments(parameterDefinition.ValueParser.RawArguments)
                    )
                );
            }

            FrozenDictionary<ReadOnlyMemory<char>, ParameterParser> parsedBindings = parsedBindingsTmp.ToFrozenDictionary(ReadOnlyMemoryCharComparer.Instance);

            QueryParsingConfig config = routeScopeBuilder.Metadata.GetOrDefault(QueryParsingConfig.Default);

            return async (RequestContext context, CallNextHandlerDelegate next) =>
            {
                using QueryStringParser queryStringParser = new(context, parsedBindings, config);

                await queryStringParser.Parse();

                return await next();
            };
        }
        #endregion

        extension<TBuilder>(TBuilder routeScopeBuilder) where TBuilder : RouteScopeBuilder
        {
            /// <summary>
            /// Updates the query-parsing configuration visible from the current builder scope.
            /// </summary>
            /// <param name="configure">
            /// A callback that receives the current configuration and returns the replacement configuration.
            /// </param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// The configuration is stored in <see cref="RouteScopeBuilder.Metadata"/>. Child builders created after this
            /// method is called inherit the updated configuration; existing child builders keep their own scoped copy.
            /// Registered query-binding middleware snapshots the configuration that is current at registration time.
            /// </remarks>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="configure"/>, or the value returned
            /// by <paramref name="configure"/> is <see langword="null"/>.
            /// </exception>
            /// <example>
            /// <code>
            /// builder.ConfigureQueryParsing(config =&gt; config with
            /// {
            ///     UnexpectedParameterBehavior = UnexpectedParameterBehavior.Reject
            /// });
            /// </code>
            /// </example>
            public TBuilder ConfigureQueryParsing(ConfigureBuilderDelegate<QueryParsingConfig> configure)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(configure);

                QueryParsingConfig config = configure(routeScopeBuilder.Metadata.GetOrDefault(QueryParsingConfig.Default));
                Ensure.NotNull(config);

                routeScopeBuilder.Metadata.Set(config);

                return routeScopeBuilder;
            }

            /// <summary>
            /// Parses configured query parameters and stores their values in <see cref="RequestContext.Parameters"/>.
            /// </summary>
            /// <param name="bindings">
            /// A query-parameter descriptor such as <c>{filter:str(min=3)}&amp;{page?:int(min=1)}</c>.
            /// </param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// This overload uses <see cref="RouteScopeBuilder.CurrentPrefix"/> as the route pattern, so the query-binding
            /// middleware is bound to the whole current builder scope for all supported HTTP methods.
            /// </remarks>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="bindings"/> references a value parser that is not registered.
            /// </exception>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> or <paramref name="bindings"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="bindings"/> has invalid query-binding syntax.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the query string is invalid for the configured bindings.</exception>
            /// <example>
            /// <code>
            /// builder.AddQueryBindings("{filter?:str}&amp;{page?:int(min=1)}");
            /// </code>
            /// </example>
            public TBuilder AddQueryBindings(string bindings) => routeScopeBuilder.AddQueryBindings(HttpVerb.Names, RouteScopeBuilder.CurrentPrefix, bindings);

            /// <summary>
            /// Parses configured query parameters and stores their values in <see cref="RequestContext.Parameters"/>.
            /// </summary>
            /// <param name="verbs">The HTTP methods that activate the query-binding middleware.</param>
            /// <param name="bindings">
            /// A query-parameter descriptor such as <c>{filter:str(min=3)}&amp;{page?:int(min=1)}</c>.
            /// </param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// This overload uses <see cref="RouteScopeBuilder.CurrentPrefix"/> as the route pattern, so the query-binding
            /// middleware is bound to the whole current builder scope for the selected HTTP methods.
            /// </remarks>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="bindings"/> references a value parser that is not registered.
            /// </exception>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, or <paramref name="bindings"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not supported or <paramref name="bindings"/> has invalid query-binding syntax.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the query string is invalid for the configured bindings.</exception>
            /// <example>
            /// <code>
            /// builder.AddQueryBindings(["GET", "HEAD"], "{filter?:str}&amp;{page?:int(min=1)}");
            /// </code>
            /// </example>
            public TBuilder AddQueryBindings(IEnumerable<string> verbs, string bindings) => routeScopeBuilder.AddQueryBindings(verbs, RouteScopeBuilder.CurrentPrefix, bindings);

            /// <summary>
            /// Parses configured query parameters and stores their values in <see cref="RequestContext.Parameters"/>.
            /// </summary>
            /// <param name="pattern">
            /// The route pattern where the query-binding middleware should be inserted. Use <c>/</c> to apply it to
            /// the whole pipeline, or a narrower prefix/exact pattern to scope query binding to selected routes.
            /// </param>
            /// <param name="bindings">
            /// A query-parameter descriptor such as <c>{filter:str(min=3)}&amp;{page?:int(min=1)}</c>.
            /// </param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="bindings"/> references a value parser that is not registered.
            /// </exception>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="pattern"/>, or <paramref name="bindings"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> has invalid route-template syntax or <paramref name="bindings"/> has invalid query-binding syntax.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the query string is invalid for the configured bindings.</exception>
            /// <example>
            /// <code>
            /// builder.AddQueryBindings("/items/*", "{filter?:str}&amp;{page?:int(min=1)}");
            /// </code>
            /// </example>
            public TBuilder AddQueryBindings(string pattern, string bindings) => routeScopeBuilder.AddQueryBindings(HttpVerb.Names, pattern, bindings);

            /// <summary>
            /// Parses configured query parameters and stores their values in <see cref="RequestContext.Parameters"/>.
            /// </summary>
            /// <param name="verb">The HTTP method that activates the query-binding middleware.</param>
            /// <param name="pattern">
            /// The route pattern where the query-binding middleware should be inserted. Use <c>/</c> to apply it to
            /// the whole pipeline, or a narrower prefix/exact pattern to scope query binding to selected routes.
            /// </param>
            /// <param name="bindings">
            /// A query-parameter descriptor such as <c>{filter:str(min=3)}&amp;{page?:int(min=1)}</c>.
            /// </param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="bindings"/> references a value parser that is not registered.
            /// </exception>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verb"/>, <paramref name="pattern"/>, or <paramref name="bindings"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not supported, <paramref name="pattern"/> has invalid route-template syntax, or <paramref name="bindings"/> has invalid query-binding syntax.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the query string is invalid for the configured bindings.</exception>
            /// <example>
            /// <code>
            /// builder.AddQueryBindings("GET", "/items/*", "{filter?:str}&amp;{page?:int(min=1)}");
            /// </code>
            /// </example>
            public TBuilder AddQueryBindings(string verb, string pattern, string bindings) => routeScopeBuilder.AddQueryBindings([verb /*will be null checked*/], pattern, bindings);

            /// <summary>
            /// Parses configured query parameters and stores their values in <see cref="RequestContext.Parameters"/>.
            /// </summary>
            /// <param name="verbs">The HTTP methods that activate the query-binding middleware.</param>
            /// <param name="pattern">
            /// The route pattern where the query-binding middleware should be inserted. Use <c>/</c> to apply it to
            /// the whole pipeline, or a narrower prefix/exact pattern to scope query binding to selected routes.
            /// </param>
            /// <param name="bindings">
            /// A query-parameter descriptor such as <c>{filter:str(min=3)}&amp;{page?:int(min=1)}</c>.
            /// </param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="bindings"/> references a value parser that is not registered.
            /// </exception>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, <paramref name="pattern"/>, or <paramref name="bindings"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not supported, <paramref name="pattern"/> has invalid route-template syntax, or <paramref name="bindings"/> has invalid query-binding syntax.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the query string is invalid for the configured bindings.</exception>
            /// <example>
            /// <code>
            /// builder.AddQueryBindings(["GET", "HEAD"], "/items/*", "{filter?:str}&amp;{page?:int(min=1)}");
            /// </code>
            /// </example>
            public TBuilder AddQueryBindings(IEnumerable<string> verbs, string pattern, string bindings)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(verbs);
                Ensure.NotNull(pattern);

                routeScopeBuilder.AddHandler(verbs, pattern, CreateHandler(routeScopeBuilder, bindings));

                return routeScopeBuilder;
            }
        }

        extension(EndpointBuilder endPointBuilder)
        {
            /// <summary>
            /// Parses configured query parameters and stores their values in <see cref="RequestContext.Parameters"/>
            /// for the current endpoint.
            /// </summary>
            /// <param name="bindings">
            /// A query-parameter descriptor such as <c>{filter:str(min=3)}&amp;{page?:int(min=1)}</c>.
            /// </param>
            /// <returns>The current <paramref name="endPointBuilder"/> instance.</returns>
            /// <remarks>
            /// The query-binding middleware is registered for the endpoint's captured HTTP methods and route match
            /// kind. Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="bindings"/> references a value parser that is not registered.
            /// </exception>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="endPointBuilder"/> or <paramref name="bindings"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when the endpoint's captured HTTP method is not supported or <paramref name="bindings"/> has invalid query-binding syntax.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the query string is invalid for the configured bindings.</exception>
            /// <example>
            /// <code>
            /// endpoint.WithQueryBindings("{filter?:str}&amp;{page?:int(min=1)}");
            /// </code>
            /// </example>
            public EndpointBuilder WithQueryBindings(string bindings)
            {
                Ensure.NotNull(endPointBuilder);

                return endPointBuilder.WithHandler
                (
                    CreateHandler(endPointBuilder.Prefix, bindings)
                );
            }
        }
    }
}
