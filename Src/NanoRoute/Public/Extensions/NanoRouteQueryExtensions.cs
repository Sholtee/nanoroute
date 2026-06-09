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
    /// builder.AddQueryBindings("{filter:str(min=3)}", unexpected: UnexpectedParameterBehavior.Reject);
    /// </code>
    /// </example>
    public enum UnexpectedParameterBehavior
    {
        /// <summary>
        /// Ignore undeclared query-string parameters.
        /// </summary>
        /// <example>
        /// <code>
        /// builder.AddQueryBindings("{filter:str(min=3)}", unexpected: UnexpectedParameterBehavior.Ignore);
        /// </code>
        /// </example>
        Ignore,

        /// <summary>
        /// Reject undeclared query-string parameters with a <c>400 Bad Request</c> error.
        /// </summary>
        /// <example>
        /// <code>
        /// builder.AddQueryBindings("{filter:str(min=3)}", unexpected: UnexpectedParameterBehavior.Reject);
        /// </code>
        /// </example>
        Reject,

        // Dangerous since it lets the caller override parameter values provided by earlier middlewares

        //AcceptAsString
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
        private static RequestHandlerDelegate CreateHandler(RouteScopeBuilder routeScopeBuilder, string bindings, UnexpectedParameterBehavior unexpected)
        {
            Ensure.NotNull(bindings);

            if (!Enum.IsDefined(typeof(UnexpectedParameterBehavior), unexpected))
                throw new ArgumentOutOfRangeException(nameof(unexpected));

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

            return async (RequestContext context, CallNextHandlerDelegate next) =>
            {
                using QueryStringParser queryStringParser = new(context, parsedBindings, unexpected);

                await queryStringParser.Parse().ConfigureAwait(false);

                return await next().ConfigureAwait(false);
            };
        }
        #endregion

        extension<TBuilder>(TBuilder routeScopeBuilder) where TBuilder : RouteScopeBuilder
        {
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
            public TBuilder AddQueryBindings(string bindings) => routeScopeBuilder.AddQueryBindings(bindings, UnexpectedParameterBehavior.Ignore);

            /// <summary>
            /// Parses configured query parameters and stores their values in <see cref="RequestContext.Parameters"/>.
            /// </summary>
            /// <param name="bindings">
            /// A query-parameter descriptor such as <c>{filter:str(min=3)}&amp;{page?:int(min=1)}</c>.
            /// </param>
            /// <param name="unexpected">Defines how undeclared query-string parameters are handled.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="bindings"/> references a value parser that is not registered.
            /// </exception>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> or <paramref name="bindings"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unexpected"/> is not a defined <see cref="UnexpectedParameterBehavior"/> value.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="bindings"/> has invalid query-binding syntax.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the query string is invalid for the configured bindings.</exception>
            /// <example>
            /// <code>
            /// builder.AddQueryBindings("{filter?:str}&amp;{page?:int(min=1)}", unexpected: UnexpectedParameterBehavior.Reject);
            /// </code>
            /// </example>
            public TBuilder AddQueryBindings(string bindings, UnexpectedParameterBehavior unexpected) =>
                routeScopeBuilder.AddQueryBindings(HttpVerb.Names, RouteScopeBuilder.CurrentPrefix, bindings, unexpected);

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
            public TBuilder AddQueryBindings(IEnumerable<string> verbs, string bindings) => routeScopeBuilder.AddQueryBindings(verbs, bindings, UnexpectedParameterBehavior.Ignore);

            /// <summary>
            /// Parses configured query parameters and stores their values in <see cref="RequestContext.Parameters"/>.
            /// </summary>
            /// <param name="verbs">The HTTP methods that activate the query-binding middleware.</param>
            /// <param name="bindings">
            /// A query-parameter descriptor such as <c>{filter:str(min=3)}&amp;{page?:int(min=1)}</c>.
            /// </param>
            /// <param name="unexpected">Defines how undeclared query-string parameters are handled.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="bindings"/> references a value parser that is not registered.
            /// </exception>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, or <paramref name="bindings"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unexpected"/> is not a defined <see cref="UnexpectedParameterBehavior"/> value.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not supported or <paramref name="bindings"/> has invalid query-binding syntax.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the query string is invalid for the configured bindings.</exception>
            /// <example>
            /// <code>
            /// builder.AddQueryBindings(["GET", "HEAD"], "{filter?:str}&amp;{page?:int(min=1)}", unexpected: UnexpectedParameterBehavior.Reject);
            /// </code>
            /// </example>
            public TBuilder AddQueryBindings(IEnumerable<string> verbs, string bindings, UnexpectedParameterBehavior unexpected) =>
                routeScopeBuilder.AddQueryBindings(verbs, RouteScopeBuilder.CurrentPrefix, bindings, unexpected);

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
            public TBuilder AddQueryBindings(string pattern, string bindings) => routeScopeBuilder.AddQueryBindings(pattern, bindings, UnexpectedParameterBehavior.Ignore);

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
            /// <param name="unexpected">Defines how undeclared query-string parameters are handled.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="bindings"/> references a value parser that is not registered.
            /// </exception>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="pattern"/>, or <paramref name="bindings"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unexpected"/> is not a defined <see cref="UnexpectedParameterBehavior"/> value.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> has invalid route-template syntax or <paramref name="bindings"/> has invalid query-binding syntax.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the query string is invalid for the configured bindings.</exception>
            /// <example>
            /// <code>
            /// builder.AddQueryBindings("/items/*", "{filter?:str}&amp;{page?:int(min=1)}", unexpected: UnexpectedParameterBehavior.Reject);
            /// </code>
            /// </example>
            public TBuilder AddQueryBindings(string pattern, string bindings, UnexpectedParameterBehavior unexpected) =>
                routeScopeBuilder.AddQueryBindings(HttpVerb.Names, pattern, bindings, unexpected);

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
            public TBuilder AddQueryBindings(string verb, string pattern, string bindings) => routeScopeBuilder.AddQueryBindings(verb, pattern, bindings, UnexpectedParameterBehavior.Ignore);

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
            /// <param name="unexpected">Defines how undeclared query-string parameters are handled.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="bindings"/> references a value parser that is not registered.
            /// </exception>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verb"/>, <paramref name="pattern"/>, or <paramref name="bindings"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unexpected"/> is not a defined <see cref="UnexpectedParameterBehavior"/> value.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not supported, <paramref name="pattern"/> has invalid route-template syntax, or <paramref name="bindings"/> has invalid query-binding syntax.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the query string is invalid for the configured bindings.</exception>
            /// <example>
            /// <code>
            /// builder.AddQueryBindings("GET", "/items/*", "{filter?:str}&amp;{page?:int(min=1)}", unexpected: UnexpectedParameterBehavior.Reject);
            /// </code>
            /// </example>
            public TBuilder AddQueryBindings(string verb, string pattern, string bindings, UnexpectedParameterBehavior unexpected) =>
                routeScopeBuilder.AddQueryBindings([verb /*will be null checked*/], pattern, bindings, unexpected);

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
            public TBuilder AddQueryBindings(IEnumerable<string> verbs, string pattern, string bindings) =>
                routeScopeBuilder.AddQueryBindings(verbs, pattern, bindings, UnexpectedParameterBehavior.Ignore);

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
            /// <param name="unexpected">Defines how undeclared query-string parameters are handled.</param>
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
            /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unexpected"/> is not a defined <see cref="UnexpectedParameterBehavior"/> value.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not supported, <paramref name="pattern"/> has invalid route-template syntax, or <paramref name="bindings"/> has invalid query-binding syntax.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the query string is invalid for the configured bindings.</exception>
            /// <example>
            /// <code>
            /// builder.AddQueryBindings(["GET", "HEAD"], "/items/*", "{filter?:str}&amp;{page?:int(min=1)}", unexpected: UnexpectedParameterBehavior.Reject);
            /// </code>
            /// </example>
            public TBuilder AddQueryBindings(IEnumerable<string> verbs, string pattern, string bindings, UnexpectedParameterBehavior unexpected)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(verbs);
                Ensure.NotNull(pattern);

                routeScopeBuilder.AddHandler(verbs, pattern, CreateHandler(routeScopeBuilder, bindings, unexpected));

                return routeScopeBuilder;
            }
        }

        extension(EndpointBuilder endpointBuilder)
        {
            /// <summary>
            /// Parses configured query parameters and stores their values in <see cref="RequestContext.Parameters"/>
            /// for the current endpoint.
            /// </summary>
            /// <param name="bindings">
            /// A query-parameter descriptor such as <c>{filter:str(min=3)}&amp;{page?:int(min=1)}</c>.
            /// </param>
            /// <returns>The current <paramref name="endpointBuilder"/> instance.</returns>
            /// <remarks>
            /// The query-binding middleware is registered for the endpoint's captured HTTP methods and route match
            /// kind. Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="bindings"/> references a value parser that is not registered.
            /// </exception>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="endpointBuilder"/> or <paramref name="bindings"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when the endpoint's captured HTTP method is not supported or <paramref name="bindings"/> has invalid query-binding syntax.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the query string is invalid for the configured bindings.</exception>
            /// <example>
            /// <code>
            /// endpoint.WithQueryBindings("{filter?:str}&amp;{page?:int(min=1)}");
            /// </code>
            /// </example>
            public EndpointBuilder WithQueryBindings(string bindings) => endpointBuilder.WithQueryBindings(bindings, UnexpectedParameterBehavior.Ignore);

            /// <summary>
            /// Parses configured query parameters and stores their values in <see cref="RequestContext.Parameters"/>
            /// for the current endpoint.
            /// </summary>
            /// <param name="bindings">
            /// A query-parameter descriptor such as <c>{filter:str(min=3)}&amp;{page?:int(min=1)}</c>.
            /// </param>
            /// <param name="unexpected">Defines how undeclared query-string parameters are handled.</param>
            /// <returns>The current <paramref name="endpointBuilder"/> instance.</returns>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="bindings"/> references a value parser that is not registered.
            /// </exception>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="endpointBuilder"/> or <paramref name="bindings"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unexpected"/> is not a defined <see cref="UnexpectedParameterBehavior"/> value.</exception>
            /// <exception cref="ArgumentException">Thrown when the endpoint's captured HTTP method is not supported or <paramref name="bindings"/> has invalid query-binding syntax.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the query string is invalid for the configured bindings.</exception>
            /// <example>
            /// <code>
            /// endpoint.WithQueryBindings("{filter?:str}&amp;{page?:int(min=1)}", unexpected: UnexpectedParameterBehavior.Reject);
            /// </code>
            /// </example>
            public EndpointBuilder WithQueryBindings(string bindings, UnexpectedParameterBehavior unexpected)
            {
                Ensure.NotNull(endpointBuilder);

                return endpointBuilder.WithHandler
                (
                    CreateHandler(endpointBuilder.Prefix, bindings, unexpected)
                );
            }
        }
    }
}
