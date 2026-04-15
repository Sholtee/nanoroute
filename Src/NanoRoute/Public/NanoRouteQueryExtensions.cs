/********************************************************************************
* NanoRouteQueryExtensions.cs                                                   *
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
            /// A case-insensitive map where the key is the target parameter name and the value is a value-parser
            /// specification. Prefix the parser specification with <c>?</c> to mark the query parameter as optional.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            public TBuilder AddQueryBindings(IReadOnlyDictionary<string, string> bindings)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(bindings);

                return routeBuilder.AddQueryBindings
                (
                    Enum.GetNames(typeof(HttpVerb)),
                    "/",
                    bindings
                );
            }

            /// <summary>
            /// Parses configured query parameters and stores their values in <see cref="RequestContext.Parameters"/>.
            /// </summary>
            /// <param name="verbs">The HTTP methods that activate the query-binding middleware.</param>
            /// <param name="bindings">
            /// A case-insensitive map where the key is the target parameter name and the value is a value-parser
            /// specification. Prefix the parser specification with <c>?</c> to mark the query parameter as optional.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            public TBuilder AddQueryBindings(IEnumerable<string> verbs, IReadOnlyDictionary<string, string> bindings)
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
            /// A case-insensitive map where the key is the target parameter name and the value is a value-parser
            /// specification. Prefix the parser specification with <c>?</c> to mark the query parameter as optional.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            public TBuilder AddQueryBindings(string pattern, IReadOnlyDictionary<string, string> bindings)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(pattern);
                Ensure.NotNull(bindings);

                return routeBuilder.AddQueryBindings
                (
                    Enum.GetNames(typeof(HttpVerb)),
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
            /// A case-insensitive map where the key is the target parameter name and the value is a value-parser
            /// specification. Prefix the parser specification with <c>?</c> to mark the query parameter as optional.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            public TBuilder AddQueryBindings(string verb, string pattern, IReadOnlyDictionary<string, string> bindings)
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
            /// A case-insensitive map where the key is the target parameter name and the value is a value-parser
            /// specification. Prefix the parser specification with <c>?</c> to mark the query parameter as optional.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Parsed query values are written into <see cref="RequestContext.Parameters"/>. If that dictionary
            /// already contains the same key because of route binding, JSON binding, or earlier middleware, the
            /// query binding overwrites the existing value.
            /// </remarks>
            public TBuilder AddQueryBindings(IEnumerable<string> verbs, string pattern, IReadOnlyDictionary<string, string> bindings)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(verbs);
                Ensure.NotNull(pattern);
                Ensure.NotNull(bindings);

                Dictionary<string, QueryParameterDefinition> parsedBindings = new(StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, string> binding in bindings)
                {
                    if (!SegmentParserDefinition.IsValidParameterName(binding.Key))
                        throw new ArgumentException(Resources.ERR_INVALID_QUERY_BINDINGS, nameof(bindings));

                    Ensure.NotNull(binding.Value, $"{nameof(binding)}.{nameof(binding.Value)}");

                    bool optional = binding.Value.StartsWith("?", StringComparison.Ordinal);

                    ValueParserDefinition valueParserDefinition = ValueParserDefinition.Create(optional ? binding.Value.Substring(1) : binding.Value);

                    if (!routeBuilder.ValueParsers.TryGetValue(valueParserDefinition.Name, out ValueParserRegistration? parserRegistration))
                        throw new InvalidOperationException
                        (
                            string.Format(Resources.Culture, Resources.ERR_NO_SUCH_PARSER, valueParserDefinition.Name)
                        );

                    parsedBindings.Add
                    (
                        binding.Key,
                        new QueryParameterDefinition
                        (
                            binding.Key,
                            parsedBindings.Count,
                            optional,
                            new ValueParser
                            (
                                valueParserDefinition,
                                parserRegistration.Parse,
                                parserRegistration.BindArguments(valueParserDefinition.RawArguments)
                            )
                        )
                    );
                }

                routeBuilder.AddHandler(verbs, pattern, async (RequestContext context, CallNextHandlerDelegate next) =>
                {
                    await QueryStringParser.Parse
                    (
                        context,
                        parsedBindings
                    );

                    return await next();
                });

                return routeBuilder;
            }
        }
    }
}
