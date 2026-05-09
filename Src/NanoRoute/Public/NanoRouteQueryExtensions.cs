/********************************************************************************
* NanoRouteQueryExtensions.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Collections.Frozen;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// Adds query-parameter binding helpers to NanoRoute.
    /// </summary>
    public static class NanoRouteQueryExtensions
    {
        extension<TBuilder>(TBuilder routeBuilder) where TBuilder : RouteBuilder
        {
            /// <summary>
            /// Parses configured query parameters and stores their values in <see cref="RequestContext.Parameters"/>.
            /// </summary>
            /// <param name="bindings">
            /// A query-parameter descriptor such as <c>{filter:str(min=3)}&amp;{page?:int(min=1)}</c>.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            public TBuilder AddQueryBindings(string bindings)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(bindings);

                return routeBuilder.AddQueryBindings
                (
                    HttpVerb.Names,
                    "/",
                    bindings
                );
            }

            /// <summary>
            /// Parses configured query parameters and stores their values in <see cref="RequestContext.Parameters"/>.
            /// </summary>
            /// <param name="verbs">The HTTP methods that activate the query-binding middleware.</param>
            /// <param name="bindings">
            /// A query-parameter descriptor such as <c>{filter:str(min=3)}&amp;{page?:int(min=1)}</c>.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            public TBuilder AddQueryBindings(IEnumerable<string> verbs, string bindings)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(verbs);
                Ensure.NotNull(bindings);

                return routeBuilder.AddQueryBindings(verbs, "/", bindings);
            }

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
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            public TBuilder AddQueryBindings(string pattern, string bindings)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(pattern);
                Ensure.NotNull(bindings);

                return routeBuilder.AddQueryBindings
                (
                    HttpVerb.Names,
                    pattern,
                    bindings
                );
            }

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
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            public TBuilder AddQueryBindings(string verb, string pattern, string bindings)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(verb);
                Ensure.NotNull(pattern);
                Ensure.NotNull(bindings);

                return routeBuilder.AddQueryBindings([verb], pattern, bindings);
            }

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
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            public TBuilder AddQueryBindings(IEnumerable<string> verbs, string pattern, string bindings)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(verbs);
                Ensure.NotNull(pattern);
                Ensure.NotNull(bindings);

                Dictionary<ReadOnlyMemory<char>, ParameterParser> parsedBindingsTmp = new(ReadOnlyMemoryCharComparer.Instance);

                foreach (ParameterDefinition parameterDefinition in RoutePatternParser.ParseQueryPattern(bindings))
                {
                    if (!routeBuilder.ValueParsers.TryGetValue(parameterDefinition.ValueParser.Name, out ValueParserRegistration? parserRegistration))
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

                routeBuilder.AddHandler(verbs, pattern, async (RequestContext context, CallNextHandlerDelegate next) =>
                {
                    using QueryStringParser queryStringParser = new(context, parsedBindings);

                    await queryStringParser.Parse();

                    return await next();
                });

                return routeBuilder;
            }
        }
    }
}
