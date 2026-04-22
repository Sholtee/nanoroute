/********************************************************************************
* RouterBuilderValueParserExtensions.cs                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// Provides convenience methods for registering value parsers.
    /// </summary>
    public static class RouterBuilderValueParserExtensions
    {
        private readonly record struct IntParserArguments(int? Min, int? Max);

        private readonly record struct StringParserArguments(int? Min, int? Max, Regex? Pattern);

        private static readonly ValueTask<ValueParseResult> s_false = new(new ValueParseResult(false, null));

        private static object? NoArgs(IReadOnlyDictionary<string, string> rawArgs)
        {
            if (rawArgs.Count > 0)
                throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, nameof(rawArgs));

            return null;
        }

        private static int ParseIntArgument(string value, string paramName)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, paramName);

            return result;
        }

        private static Regex ParseRegexArgument(string value, string paramName)
        {
            try
            {
                return new Regex(value);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, paramName, ex);
            }
        }

        extension<TBuilder>(TBuilder routeBuilder) where TBuilder : RouteBuilder
        {
            /// <summary>
            /// Registers a synchronous parser by adapting it to <see cref="ValueParserDelegate"/>.
            /// </summary>
            /// <param name="parserName">The name used in route patterns such as <c>{id:int}</c>.</param>
            /// <param name="tryParseDelegate">The synchronous parser to adapt.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddValueParser(string parserName, SyncValueParserDelegate tryParseDelegate)
            {
                return routeBuilder.AddValueParser(parserName, NoArgs, tryParseDelegate);
            }

            /// <summary>
            /// Registers a synchronous parser by adapting it to <see cref="ValueParserDelegate"/> and binding parser arguments once during route registration.
            /// </summary>
            /// <param name="parserName">The name used in route patterns such as <c>{id:int(min=1)}</c>.</param>
            /// <param name="bindArguments">Converts raw parser arguments into typed values once per route-template branch.</param>
            /// <param name="tryParseDelegate">The synchronous parser to adapt.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddValueParser(string parserName, BindArgumentsDelegate bindArguments, SyncValueParserDelegate tryParseDelegate)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(parserName);
                Ensure.NotNull(bindArguments);
                Ensure.NotNull(tryParseDelegate);

                routeBuilder.AddValueParser(parserName, bindArguments, context =>
                {
                    bool success = tryParseDelegate(context.Segment, context.Arguments, out object? parsed);
                    return new ValueTask<ValueParseResult>(new ValueParseResult(success, parsed));
                });

                return routeBuilder;
            }

            /// <summary>
            /// Registers an asynchronous parser without route-template arguments.
            /// </summary>
            /// <param name="parserName">The name used in route patterns such as <c>{id:user}</c>.</param>
            /// <param name="tryParseDelegate">The asynchronous parser to register.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddValueParser(string parserName, ValueParserDelegate tryParseDelegate)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(parserName);
                Ensure.NotNull(tryParseDelegate);

                routeBuilder.AddValueParser(parserName, NoArgs, tryParseDelegate);

                return routeBuilder;
            }

            /// <summary>
            /// Registers an asynchronous parser and binds parser arguments once during route registration.
            /// </summary>
            /// <param name="parserName">The name used in route patterns such as <c>{id:user(scope='admins')}</c>.</param>
            /// <param name="bindArguments">Converts raw parser arguments into a parser-specific payload.</param>
            /// <param name="tryParseDelegate">The asynchronous parser to register.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddValueParser(string parserName, BindArgumentsDelegate bindArguments, ValueParserDelegate tryParseDelegate)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(parserName);
                Ensure.NotNull(bindArguments);
                Ensure.NotNull(tryParseDelegate);

                routeBuilder.AddValueParser(parserName, bindArguments, tryParseDelegate);

                return routeBuilder;
            }

            /// <summary>
            /// Registers the built-in <c>int</c> value parser.
            /// </summary>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Supported arguments:
            /// <c>min</c>, <c>max</c>.
            /// </remarks>
            public TBuilder AddIntParser()
            {
                Ensure.NotNull(routeBuilder);

                routeBuilder.AddValueParser
                (
                    "int",
                    bindArguments: static (IReadOnlyDictionary<string, string> args) =>
                    {
                        int?
                            min = null,
                            max = null;

                        foreach (KeyValuePair<string, string> arg in args)
                        {
                            switch (arg.Key.ToLower())
                            {
                                case "min":
                                    min = ParseIntArgument(arg.Value, nameof(args));
                                    break;
                                case "max":
                                    max = ParseIntArgument(arg.Value, nameof(args));
                                    break;
                                default:
                                    throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, nameof(args));
                            }
                        }

                        if (min > max)
                            throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, nameof(args));

                        return new IntParserArguments(min, max);
                    },
                    tryParseDelegate: static (ReadOnlyMemory<char> segment, object? arguments, out object? parsed) =>
                    {
                        IntParserArguments args = (IntParserArguments) arguments!;
                        parsed = null;

                        if (!int.TryParse(segment.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                            return false;

                        if (value < args.Min)
                            return false;

                        if (value > args.Max)
                            return false;

                        parsed = value;
                        return true;
                    }
                );

                return routeBuilder;
            }

            /// <summary>
            /// Registers the built-in <c>guid</c> value parser.
            /// </summary>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// This parser does not support any arguments.
            /// </remarks>
            public TBuilder AddGuidParser() => routeBuilder.AddValueParser
            (
                "guid",
                static (ReadOnlyMemory<char> segment, object? _, out object? parsed) =>
                {
                    bool success = Guid.TryParse(segment.ToString(), out Guid value);
                    parsed = success ? value : null;
                    return success;
                }
            );

            /// <summary>
            /// Registers the built-in <c>bool</c> value parser.
            /// </summary>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// This parser does not support any arguments.
            /// </remarks>
            public TBuilder AddBoolParser() => routeBuilder.AddValueParser
            (
                "bool",
                static (ReadOnlyMemory<char> segment, object? _, out object? parsed) =>
                {
                    bool success = bool.TryParse(segment.ToString(), out bool value);
                    parsed = success ? value : null;
                    return success;
                }
            );

            /// <summary>
            /// Registers the built-in <c>str</c> value parser.
            /// </summary>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Supported arguments:
            /// <c>min</c>, <c>max</c>, <c>pattern</c>.
            /// </remarks>
            public TBuilder AddStringParser()
            {
                Ensure.NotNull(routeBuilder);

                routeBuilder.AddValueParser
                (
                    "str",
                    bindArguments: static (IReadOnlyDictionary<string, string> args) =>
                    {
                        int?
                            min = null,
                            max = null;
                        Regex? pattern = null;

                        foreach (KeyValuePair<string, string> arg in args)
                        {
                            switch (arg.Key.ToLower())
                            {
                                case "min":
                                    min = ParseIntArgument(arg.Value, nameof(args));
                                    break;
                                case "max":
                                    max = ParseIntArgument(arg.Value, nameof(args));
                                    break;
                                case "pattern":
                                    pattern = ParseRegexArgument(arg.Value, nameof(args));
                                    break;
                                default:
                                    throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, nameof(args));
                            }                 
                        }

                        if (min > max)
                            throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, nameof(args));

                        return new StringParserArguments(min, max, pattern);
                    },
                    tryParseDelegate: static (ValueParserContext context) =>
                    {
                        StringParserArguments args = (StringParserArguments) context.Arguments!;

                        if (context.Segment.Length < args.Min)
                            return s_false;

                        if (context.Segment.Length > args.Max)
                            return s_false;

                        string segmentStr = context.Segment.ToString();

                        if (args.Pattern?.IsMatch(segmentStr) is false)
                            return s_false;

                        return new ValueTask<ValueParseResult>(new ValueParseResult(true, segmentStr));
                    }
                );

                return routeBuilder;
            }

            /// <summary>
            /// Registers the built-in value parsers for common scalar route segments.
            /// </summary>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// This convenience method registers parsers named <c>int</c>, <c>guid</c>, <c>bool</c>, and <c>str</c>.
            /// Existing registrations with the same names are overwritten.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeBuilder"/> is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// builder
            ///     .AddDefaultValueParsers()
            ///     .AddHandler("GET", "/users/{id:int}", (context, next) =&gt; Results.Ok(context.Parameters["id"]));
            /// </code>
            /// </example>
            public TBuilder AddDefaultValueParsers()
            {
                Ensure.NotNull(routeBuilder);

                routeBuilder
                    .AddIntParser()
                    .AddGuidParser()
                    .AddBoolParser()
                    .AddStringParser();

                return routeBuilder;
            }
        }
    }
}


