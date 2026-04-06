/********************************************************************************
* RouterBuilderSegmentParserExtensions.cs                                       *
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
    /// Provides convenience methods for registering segment parsers.
    /// </summary>
    /// <remarks>
    /// These helpers build on top of <see cref="RouteBuilder.AddSegmentParser(string, SegmentParserDelegate)"/>.
    /// </remarks>
    public static class RouterBuilderSegmentParserExtensions
    {
        private readonly record struct IntParserArguments(int? Min, int? Max);

        private readonly record struct StringParserArguments(int? Min, int? Max, Regex? Pattern);

        private static readonly SegmentParseResult s_parseFailed = new(false, null);

        private static object? NoArgs(IReadOnlyDictionary<string, string> rawArgs)
        {
            if (rawArgs.Count > 0)
                throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, nameof(rawArgs));

            return null;
        }

        extension<TBuilder>(TBuilder routeBuilder) where TBuilder : RouteBuilder
        {
            /// <summary>
            /// Registers a synchronous parser by adapting it to <see cref="SegmentParserDelegate"/>.
            /// </summary>
            /// <param name="parserName">The name used in route patterns such as <c>{id:int}</c>.</param>
            /// <param name="tryParseDelegate">The synchronous parser to adapt.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddSegmentParser(string parserName, SyncSegmentParserDelegate tryParseDelegate)
            {
                return routeBuilder.AddSegmentParser(parserName, NoArgs, tryParseDelegate);
            }

            /// <summary>
            /// Registers a synchronous parser by adapting it to <see cref="SegmentParserDelegate"/> and binding parser arguments once during route registration.
            /// </summary>
            /// <param name="parserName">The name used in route patterns such as <c>{id:int(min=1)}</c>.</param>
            /// <param name="bindArguments">Converts raw parser arguments into typed values once per route-template branch.</param>
            /// <param name="tryParseDelegate">The synchronous parser to adapt.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddSegmentParser(string parserName, BindArgumentsDelegate bindArguments, SyncSegmentParserDelegate tryParseDelegate)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(parserName);
                Ensure.NotNull(bindArguments);
                Ensure.NotNull(tryParseDelegate);

                routeBuilder.AddSegmentParser(parserName, bindArguments, context =>
                {
                    bool success = tryParseDelegate(context.Segment, context.Arguments, out object? parsed);
                    return new ValueTask<SegmentParseResult>(new SegmentParseResult(success, parsed));
                });

                return routeBuilder;
            }

            /// <summary>
            /// Registers an asynchronous parser without route-template arguments.
            /// </summary>
            /// <param name="parserName">The name used in route patterns such as <c>{id:user}</c>.</param>
            /// <param name="tryParseDelegate">The asynchronous parser to register.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddSegmentParser(string parserName, SegmentParserDelegate tryParseDelegate)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(parserName);
                Ensure.NotNull(tryParseDelegate);

                routeBuilder.AddSegmentParser(parserName, NoArgs, tryParseDelegate);

                return routeBuilder;
            }

            /// <summary>
            /// Registers an asynchronous parser and binds parser arguments once during route registration.
            /// </summary>
            /// <param name="parserName">The name used in route patterns such as <c>{id:user(scope='admins')}</c>.</param>
            /// <param name="bindArguments">Converts raw parser arguments into a parser-specific payload.</param>
            /// <param name="tryParseDelegate">The asynchronous parser to register.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddSegmentParser(string parserName, BindArgumentsDelegate bindArguments, SegmentParserDelegate tryParseDelegate)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(parserName);
                Ensure.NotNull(bindArguments);
                Ensure.NotNull(tryParseDelegate);

                routeBuilder.AddSegmentParser(parserName, bindArguments, tryParseDelegate);

                return routeBuilder;
            }

            /// <summary>
            /// Registers the built-in <c>int</c> segment parser.
            /// </summary>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Supported arguments:
            /// <c>min</c>, <c>max</c>.
            /// </remarks>
            public TBuilder AddIntParser()
            {
                Ensure.NotNull(routeBuilder);

                routeBuilder.AddSegmentParser
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
                                    min = int.Parse(arg.Value, CultureInfo.InvariantCulture);
                                    break;
                                case "max":
                                    max = int.Parse(arg.Value, CultureInfo.InvariantCulture);
                                    break;
                                default:
                                    throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, nameof(args));
                            }
                        }

                        if (min > max)
                            throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, nameof(args));

                        return new IntParserArguments(min, max);
                    },
                    tryParseDelegate: static (string segment, object? arguments, out object? parsed) =>
                    {
                        IntParserArguments args = (IntParserArguments) arguments!;
                        parsed = null;

                        if (!int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
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
            /// Registers the built-in <c>guid</c> segment parser.
            /// </summary>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// This parser does not support any arguments.
            /// </remarks>
            public TBuilder AddGuidParser() => routeBuilder.AddSegmentParser
            (
                "guid",
                static (string segment, object? _, out object? parsed) =>
                {
                    bool success = Guid.TryParse(segment, out Guid value);
                    parsed = success ? value : null;
                    return success;
                }
            );

            /// <summary>
            /// Registers the built-in <c>bool</c> segment parser.
            /// </summary>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// This parser does not support any arguments.
            /// </remarks>
            public TBuilder AddBoolParser() => routeBuilder.AddSegmentParser
            (
                "bool",
                static (string segment, object? _, out object? parsed) =>
                {
                    bool success = bool.TryParse(segment, out bool value);
                    parsed = success ? value : null;
                    return success;
                }
            );

            /// <summary>
            /// Registers the built-in <c>str</c> segment parser.
            /// </summary>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// Supported arguments:
            /// <c>min</c>, <c>max</c>, <c>pattern</c>.
            /// </remarks>
            public TBuilder AddStringParser()
            {
                Ensure.NotNull(routeBuilder);

                routeBuilder.AddSegmentParser
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
                                    min = int.Parse(arg.Value, CultureInfo.InvariantCulture);
                                    break;
                                case "max":
                                    max = int.Parse(arg.Value, CultureInfo.InvariantCulture);
                                    break;
                                case "pattern":
                                    pattern = new Regex(arg.Value);
                                    break;
                                default:
                                    throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, nameof(args));
                            }                 
                        }

                        if (min > max)
                            throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, nameof(args));

                        return new StringParserArguments(min, max, pattern);
                    },
                    tryParseDelegate: static (string segment, object? arguments, out object? parsed) =>
                    {
                        StringParserArguments args = (StringParserArguments) arguments!;
                        parsed = null;

                        if (segment.Length < args.Min)
                            return false;

                        if (segment.Length > args.Max)
                            return false;

                        if (args.Pattern?.IsMatch(segment) is false)
                            return false;

                        parsed = segment;
                        return true;
                    }
                );

                return routeBuilder;
            }

            /// <summary>
            /// Registers the built-in segment parsers for common scalar route segments.
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
            ///     .AddDefaultParsers()
            ///     .AddHandler("GET", "/users/{id:int}", (context, next) =&gt; Results.Ok(context.Parameters["id"]));
            /// </code>
            /// </example>
            public TBuilder AddDefaultParsers()
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

