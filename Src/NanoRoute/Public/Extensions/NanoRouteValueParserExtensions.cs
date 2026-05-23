/********************************************************************************
* NanoRouteValueParserExtensions.cs                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// Represents a synchronous value parser.
    /// </summary>
    /// <param name="segment">The decoded segment extracted from the request URI.</param>
    /// <param name="arguments">
    /// The parser-specific argument payload produced by <see cref="BindArgumentsDelegate"/> during route registration,
    /// or <see langword="null"/> when the parser was registered without arguments.
    /// </param>
    /// <param name="parsed">The parsed value when the delegate returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the segment is accepted by the parser; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Exceptions thrown by the delegate propagate during request processing for matching routes that use the parser.
    /// </remarks>
    /// <example>
    /// <code>
    /// routerBuilder.AddValueParser("int", (ReadOnlyMemory&lt;char&gt; segment, object? arguments, out object? parsed) =&gt;
    /// {
    ///     var limits = ((int? Min, int? Max)) arguments!;
    ///
    ///     if (int.TryParse(segment, out int value))
    ///     {
    ///         if (limits.Min.HasValue &amp;&amp; value &lt; limits.Min.Value)
    ///         {
    ///             parsed = null;
    ///             return false;
    ///         }
    ///
    ///         parsed = value;
    ///         return true;
    ///     }
    ///
    ///     parsed = null;
    ///     return false;
    /// });
    /// </code>
    /// </example>
    public delegate bool SyncValueParserDelegate(ReadOnlyMemory<char> segment, object? arguments, out object? parsed);

    /// <summary>
    /// Provides convenience methods for registering value parsers.
    /// </summary>
    /// <example>
    /// <code>
    /// builder
    ///     .AddDefaultValueParsers()
    ///     .AddHandler("GET", "/users/{id:int}/", (context, _) =&gt; Results.Ok(context.Parameters["id"]));
    /// </code>
    /// </example>
    public static class NanoRouteValueParserExtensions
    {
        #region Private
        private readonly record struct IntParserArguments(int? Min, int? Max);

        private readonly record struct StringParserArguments(int? Min, int? Max);

        private readonly record struct RegexParserArguments(Regex Pattern);

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

        private static bool ParseBoolArgument(string value, string paramName)
        {
            if (!bool.TryParse(value, out bool result))
                throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, paramName);

            return result;
        }

        private static Regex ParseRegexArgument(string value, bool caseSensitive, int timeoutMs, string paramName)
        {
            if (timeoutMs <= 0)
                throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, paramName);

            RegexOptions options = RuntimeFeature.IsDynamicCodeSupported
                ? RegexOptions.Compiled
                : RegexOptions.None;

            if (!caseSensitive)
                options |= RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

            try
            {
                return new Regex(value, options, TimeSpan.FromMilliseconds(timeoutMs));
            }
            catch (ArgumentException ex)  // catches ArgumentOutOfRangeException too
            {
                throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, paramName, ex);
            }
        }
        #endregion

        extension<TBuilder>(TBuilder routeScopeBuilder) where TBuilder : RouteScopeBuilder
        {
            /// <summary>
            /// Registers a synchronous parser by adapting it to <see cref="ValueParserDelegate"/>.
            /// </summary>
            /// <param name="parserName">The name used in route patterns such as <c>{id:int}</c>.</param>
            /// <param name="tryParseDelegate">The synchronous parser to adapt.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="parserName"/>, or
            /// <paramref name="tryParseDelegate"/> is <see langword="null"/>.
            /// </exception>
            /// <example>
            /// <code>
            /// builder.AddValueParser("slug", static (ReadOnlyMemory&lt;char&gt; segment, object? _, out object? parsed) =&gt;
            /// {
            ///     parsed = segment.ToString();
            ///     return segment.Length &gt; 0;
            /// });
            /// </code>
            /// </example>
            public TBuilder AddValueParser(string parserName, SyncValueParserDelegate tryParseDelegate) =>
                routeScopeBuilder.AddValueParser(parserName, NoArgs, tryParseDelegate);

            /// <summary>
            /// Registers a synchronous parser by adapting it to <see cref="ValueParserDelegate"/> and binding parser arguments once during route registration.
            /// </summary>
            /// <param name="parserName">The name used in route patterns such as <c>{id:int(min=1)}</c>.</param>
            /// <param name="bindArguments">Converts raw parser arguments into typed values once per route-template branch.</param>
            /// <param name="tryParseDelegate">The synchronous parser to adapt.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="parserName"/>,
            /// <paramref name="bindArguments"/>, or <paramref name="tryParseDelegate"/> is <see langword="null"/>.
            /// </exception>
            /// <example>
            /// <code>
            /// builder.AddValueParser("str", BindStringParserArguments, TryParseStringSegment);
            /// </code>
            /// </example>
            public TBuilder AddValueParser(string parserName, BindArgumentsDelegate bindArguments, SyncValueParserDelegate tryParseDelegate)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(parserName);
                Ensure.NotNull(bindArguments);
                Ensure.NotNull(tryParseDelegate);

                routeScopeBuilder.AddValueParser(parserName, bindArguments, context =>
                {
                    bool success = tryParseDelegate(context.Segment, context.Arguments, out object? parsed);
                    return new ValueTask<ValueParseResult>(new ValueParseResult(success, parsed));
                });

                return routeScopeBuilder;
            }

            /// <summary>
            /// Registers an asynchronous parser without route-template arguments.
            /// </summary>
            /// <param name="parserName">The name used in route patterns such as <c>{id:user}</c>.</param>
            /// <param name="tryParseDelegate">The asynchronous parser to register.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="parserName"/>, or
            /// <paramref name="tryParseDelegate"/> is <see langword="null"/>.
            /// </exception>
            /// <example>
            /// <code>
            /// builder.AddValueParser("user", static async context =&gt;
            /// {
            ///     object? user = await FindUserAsync(context.Segment.ToString(), context.Cancellation);
            ///     return new ValueParseResult(user is not null, user);
            /// });
            /// </code>
            /// </example>
            public TBuilder AddValueParser(string parserName, ValueParserDelegate tryParseDelegate)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(parserName);
                Ensure.NotNull(tryParseDelegate);

                routeScopeBuilder.AddValueParser(parserName, NoArgs, tryParseDelegate);

                return routeScopeBuilder;
            }

            /// <summary>
            /// Registers an asynchronous parser and binds parser arguments once during route registration.
            /// </summary>
            /// <param name="parserName">The name used in route patterns such as <c>{id:user(scope='admins')}</c>.</param>
            /// <param name="bindArguments">Converts raw parser arguments into a parser-specific payload.</param>
            /// <param name="tryParseDelegate">The asynchronous parser to register.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="parserName"/>,
            /// <paramref name="bindArguments"/>, or <paramref name="tryParseDelegate"/> is <see langword="null"/>.
            /// </exception>
            /// <example>
            /// <code>
            /// builder.AddValueParser("user", BindUserParserArguments, ParseUserAsync);
            /// </code>
            /// </example>
            public TBuilder AddValueParser(string parserName, BindArgumentsDelegate bindArguments, ValueParserDelegate tryParseDelegate)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(parserName);
                Ensure.NotNull(bindArguments);
                Ensure.NotNull(tryParseDelegate);

                routeScopeBuilder.AddValueParser(parserName, bindArguments, tryParseDelegate);

                return routeScopeBuilder;
            }

            /// <summary>
            /// Registers the built-in <c>int</c> value parser.
            /// </summary>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// Supported arguments:
            /// <c>min</c>, <c>max</c>.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// builder
            ///     .AddIntParser()
            ///     .AddHandler("GET", "/items/{id:int(min=1)}/", (context, _) =&gt; Results.Ok(context.Parameters["id"]));
            /// </code>
            /// </example>
            public TBuilder AddIntParser()
            {
                Ensure.NotNull(routeScopeBuilder);

                routeScopeBuilder.AddValueParser
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
#if NETSTANDARD2_1_OR_GREATER
                        if (!int.TryParse(segment.Span, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
#else
                        if (!int.TryParse(segment.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
#endif
                            return false;

                        if (value < args.Min)
                            return false;

                        if (value > args.Max)
                            return false;

                        parsed = value;
                        return true;
                    }
                );

                return routeScopeBuilder;
            }

            /// <summary>
            /// Registers the built-in <c>guid</c> value parser.
            /// </summary>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// This parser does not support any arguments.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// builder
            ///     .AddGuidParser()
            ///     .AddHandler("GET", "/users/{id:guid}/", (context, _) =&gt; Results.Ok(context.Parameters["id"]));
            /// </code>
            /// </example>
            public TBuilder AddGuidParser() => routeScopeBuilder.AddValueParser
            (
                "guid",
                static (ReadOnlyMemory<char> segment, object? _, out object? parsed) =>
                {
                    bool success =
#if NETSTANDARD2_1_OR_GREATER
                        Guid.TryParse(segment.Span, out Guid value);
#else
                        Guid.TryParse(segment.ToString(), out Guid value);
#endif
                    parsed = success ? value : null;
                    return success;
                }
            );

            /// <summary>
            /// Registers the built-in <c>bool</c> value parser.
            /// </summary>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// This parser does not support any arguments.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// builder
            ///     .AddBoolParser()
            ///     .AddHandler("GET", "/features/{enabled:bool}/", (context, _) =&gt; Results.Ok(context.Parameters["enabled"]));
            /// </code>
            /// </example>
            public TBuilder AddBoolParser() => routeScopeBuilder.AddValueParser
            (
                "bool",
                static (ReadOnlyMemory<char> segment, object? _, out object? parsed) =>
                {
                    bool success =
#if NETSTANDARD2_1_OR_GREATER
                        bool.TryParse(segment.Span, out bool value);
#else
                        bool.TryParse(segment.ToString(), out bool value);
#endif
                    parsed = success ? value : null;
                    return success;
                }
            );

            /// <summary>
            /// Registers the built-in <c>str</c> value parser.
            /// </summary>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// Supported arguments:
            /// <c>min</c>, <c>max</c>.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// builder
            ///     .AddStringParser()
            ///     .AddHandler("GET", "/users/{name:str(min=2)}/", (context, _) =&gt; Results.Ok(context.Parameters["name"]));
            /// </code>
            /// </example>
            public TBuilder AddStringParser()
            {
                Ensure.NotNull(routeScopeBuilder);

                routeScopeBuilder.AddValueParser
                (
                    "str",
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

                        return new StringParserArguments(min, max);
                    },
                    tryParseDelegate: static (ValueParserContext context) =>
                    {
                        StringParserArguments args = (StringParserArguments) context.Arguments!;

                        if (context.Segment.Length < args.Min)
                            return s_false;

                        if (context.Segment.Length > args.Max)
                            return s_false;

                        string segmentStr = context.Segment.ToString();

                        return new ValueTask<ValueParseResult>(new ValueParseResult(true, segmentStr));
                    }
                );

                return routeScopeBuilder;
            }

            /// <summary>
            /// Registers the built-in <c>regex</c> value parser.
            /// </summary>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// Supported arguments:
            /// <c>pattern</c>, <c>timeoutMs</c>, <c>caseSensitive</c>. The <c>pattern</c> argument is required,
            /// <c>timeoutMs</c> defaults to <c>50</c>, and <c>caseSensitive</c> defaults to <see langword="false"/>.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// builder
            ///     .AddRegexParser()
            ///     .AddHandler("GET", "/tags/{slug:regex(pattern='^[a-z]+$',timeoutMs=50)}/", (context, _) =&gt; Results.Ok(context.Parameters["slug"]));
            /// </code>
            /// </example>
            public TBuilder AddRegexParser()
            {
                Ensure.NotNull(routeScopeBuilder);

                routeScopeBuilder.AddValueParser
                (
                    "regex",
                    bindArguments: static (IReadOnlyDictionary<string, string> args) =>
                    {
                        string? pattern = null;
                        int timeoutMs = 50;
                        bool caseSensitive = false;

                        foreach (KeyValuePair<string, string> arg in args)
                        {
                            switch (arg.Key.ToLower())
                            {
                                case "pattern":
                                    pattern = arg.Value;
                                    break;
                                case "timeoutms":
                                    timeoutMs = ParseIntArgument(arg.Value, nameof(args));
                                    break;
                                case "casesensitive":
                                    caseSensitive = ParseBoolArgument(arg.Value, nameof(args));
                                    break;
                                default:
                                    throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, nameof(args));
                            }
                        }

                        return new RegexParserArguments(ParseRegexArgument(pattern!, caseSensitive, timeoutMs, nameof(args)));
                    },
                    tryParseDelegate: static (ValueParserContext context) =>
                    {
                        RegexParserArguments args = (RegexParserArguments) context.Arguments!;
                        string segmentStr = context.Segment.ToString();

                        try
                        {
                            if (!args.Pattern.IsMatch(segmentStr))
                                return s_false;
                        }
                        catch (RegexMatchTimeoutException)
                        {
                            return s_false;
                        }

                        return new ValueTask<ValueParseResult>(new ValueParseResult(true, segmentStr));
                    }
                );

                return routeScopeBuilder;
            }

            /// <summary>
            /// Registers the built-in value parsers for common scalar route segments.
            /// </summary>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// This convenience method registers parsers named <c>int</c>, <c>guid</c>, <c>bool</c>, <c>str</c>, and <c>regex</c>.
            /// Existing registrations with the same names are overwritten.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// builder
            ///     .AddDefaultValueParsers()
            ///     .AddHandler("GET", "/users/{id:int}/", (context, next) =&gt; Results.Ok(context.Parameters["id"]));
            /// </code>
            /// </example>
            public TBuilder AddDefaultValueParsers()
            {
                Ensure.NotNull(routeScopeBuilder);

                routeScopeBuilder
                    .AddIntParser()
                    .AddGuidParser()
                    .AddBoolParser()
                    .AddStringParser()
                    .AddRegexParser();

                return routeScopeBuilder;
            }
        }
    }
}


