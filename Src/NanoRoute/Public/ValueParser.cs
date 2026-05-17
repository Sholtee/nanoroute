/********************************************************************************
* ValueParser.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute
{
    /// <summary>
    /// Binds raw parser arguments to an opaque object that is cached with the route definition.
    /// </summary>
    /// <param name="rawArgs">
    /// The raw parser arguments as parsed from the route template, keyed case-insensitively.
    /// </param>
    /// <returns>
    /// A parser-specific object that will later be exposed through <see cref="ValueParserContext.Arguments"/>.
    /// Return <see langword="null"/> when the parser does not need a bound payload.
    /// </returns>
    /// <remarks>
    /// This delegate runs during route registration, not during request processing. It is the right place to
    /// validate parser arguments, parse numeric limits, or precompile regular expressions once.
    /// Exceptions thrown by the delegate are reported while the route containing the parser arguments is registered.
    /// </remarks>
    /// <example>
    /// <code>
    /// routerBuilder.AddValueParser
    /// (
    ///     "int",
    ///     static rawArgs =&gt;
    ///     (
    ///         Min: rawArgs.TryGetValue("min", out string? min) ? int.Parse(min) : null,
    ///         Max: rawArgs.TryGetValue("max", out string? max) ? int.Parse(max) : null
    ///     ),
    ///     static context =&gt;
    ///     {
    ///         var args = ((int? Min, int? Max)) context.Arguments!;
    ///         return ValueTask.FromResult(new ValueParseResult(true, context.Segment));
    ///     }
    /// );
    /// </code>
    /// </example>
    public delegate object? BindArgumentsDelegate(IReadOnlyDictionary<string, string> rawArgs);

    /// <summary>
    /// Tries to parse a single route segment into a value that can optionally be stored in <see cref="RequestContext.Parameters"/>.
    /// </summary>
    /// <param name="context">
    /// The parser context, including the decoded route segment, request services, and the linked pipeline cancellation token.
    /// </param>
    /// <returns>A <see cref="ValueParseResult"/> that describes whether the segment matched and what value it produced.</returns>
    /// <remarks>
    /// Exceptions thrown by the delegate propagate during request processing for matching routes that use the parser.
    /// </remarks>
    /// <example>
    /// <code>
    /// routerBuilder.AddValueParser("user", static async (ValueParserContext context) =&gt;
    /// {
    ///     if (!Guid.TryParse(context.Segment, out Guid userId))
    ///         return new ValueParseResult(false, null);
    ///
    ///     object? user = await context
    ///         .Services
    ///         .GetRequiredService&lt;IUserRepository&gt;()
    ///         .TryGetAsync(userId, context.Cancellation);
    ///
    ///     return new ValueParseResult(user is not null, user);
    /// });
    /// </code>
    /// </example>
    public delegate ValueTask<ValueParseResult> ValueParserDelegate(ValueParserContext context);

    /// <summary>
    /// Carries the current route segment and request-scoped services into an asynchronous value parser.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.AddValueParser("user", static async context =&gt;
    /// {
    ///     Guid id = Guid.Parse(context.Segment.ToString());
    ///     object? user = await context.Services.GetRequiredService&lt;IUserStore&gt;().FindAsync(id, context.Cancellation);
    ///     return new ValueParseResult(user is not null, user);
    /// });
    /// </code>
    /// </example>
    public readonly struct ValueParserContext
    {
        /// <summary>
        /// Gets the decoded route segment.
        /// </summary>
        /// <example>
        /// <code>
        /// string value = context.Segment.ToString();
        /// </code>
        /// </example>
        public required ReadOnlyMemory<char> Segment { get; init; }

        /// <summary>
        /// Gets the request-scoped service provider.
        /// </summary>
        /// <example>
        /// <code>
        /// IUserStore store = context.Services.GetRequiredService&lt;IUserStore&gt;();
        /// </code>
        /// </example>
        public required IServiceProvider Services { get; init; }

        /// <summary>
        /// Gets the parser arguments that were bound during route registration.
        /// </summary>
        /// <example>
        /// <code>
        /// ParserLimits? limits = (ParserLimits?) context.Arguments;
        /// </code>
        /// </example>
        public object? Arguments { get; init; }

        /// <summary>
        /// Gets the linked pipeline cancellation token.
        /// </summary>
        /// <example>
        /// <code>
        /// context.Cancellation.ThrowIfCancellationRequested();
        /// </code>
        /// </example>
        public CancellationToken Cancellation { get; init; }
    }

    /// <summary>
    /// Represents the outcome of a value parser.
    /// </summary>
    /// <param name="Success"><see langword="true"/> when the segment is accepted by the parser; otherwise <see langword="false"/>.</param>
    /// <param name="Parsed">The parsed value when <paramref name="Success"/> is <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <example>
    /// <code>
    /// return new ValueParseResult(true, parsedValue);
    /// </code>
    /// </example>
    public readonly record struct ValueParseResult(bool Success, object? Parsed);

    /// <summary>
    /// Stores a named value parser together with its argument binder.
    /// </summary>
    /// <example>
    /// <code>
    /// ValueParserRegistration registration = builder.ValueParsers["int"];
    /// </code>
    /// </example>
    public sealed record ValueParserRegistration(string Name, ValueParserDelegate Parse, BindArgumentsDelegate BindArguments);
}
