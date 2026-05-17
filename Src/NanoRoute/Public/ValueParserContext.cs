/********************************************************************************
* ValueParserContext.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;

namespace NanoRoute
{
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
}