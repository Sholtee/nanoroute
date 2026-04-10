/********************************************************************************
* Delegates.cs                                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace NanoRoute
{
    using Json;

    /// <summary>
    /// Binds raw parser arguments to an opaque object that is cached with the route definition.
    /// </summary>
    /// <param name="rawArgs">
    /// The raw parser arguments as parsed from the route template, keyed case-insensitively.
    /// </param>
    /// <returns>
    /// A parser-specific object that will later be exposed through <see cref="SegmentParserContext.Arguments"/>.
    /// Return <see langword="null"/> when the parser does not need a bound payload.
    /// </returns>
    /// <remarks>
    /// This delegate runs during route registration, not during request processing. It is the right place to
    /// validate parser arguments, parse numeric limits, or precompile regular expressions once.
    /// </remarks>
    /// <example>
    /// <code>
    /// routerBuilder.AddSegmentParser
    /// (
    ///     "int",
    ///     static rawArgs => (
    ///         Min: rawArgs.TryGetValue("min", out string? min) ? int.Parse(min) : null,
    ///         Max: rawArgs.TryGetValue("max", out string? max) ? int.Parse(max) : null
    ///     ),
    ///     static context =>
    ///     {
    ///         var args = ((int? Min, int? Max)) context.Arguments!;
    ///         return ValueTask.FromResult(new SegmentParseResult(true, context.Segment));
    ///     }
    /// );
    /// </code>
    /// </example>
    public delegate object? BindArgumentsDelegate(IReadOnlyDictionary<string, string> rawArgs);

    /// <summary>
    /// Represents a synchronous segment parser.
    /// </summary>
    /// <param name="segment">The raw path segment extracted from the request URI.</param>
    /// <param name="arguments">
    /// The parser-specific argument payload produced by <see cref="BindArgumentsDelegate"/> during route registration,
    /// or <see langword="null"/> when the parser was registered without arguments.
    /// </param>
    /// <param name="parsed">The parsed value when the delegate returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the segment is accepted by the parser; otherwise <see langword="false"/>.</returns>
    /// <example>
    /// <code>
    /// routerBuilder.AddSegmentParser("int", (string segment, object? arguments, out object? parsed) =&gt;
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
    public delegate bool SyncSegmentParserDelegate(ReadOnlyMemory<char> segment, object? arguments, out object? parsed);

    /// <summary>
    /// Tries to parse a single route segment into a value that can optionally be stored in <see cref="RequestContext.Parameters"/>.
    /// </summary>
    /// <param name="context">
    /// The parser context, including the decoded segment, request services, and the linked pipeline cancellation token.
    /// </param>
    /// <returns>A <see cref="SegmentParseResult"/> that describes whether the segment matched and what value it produced.</returns>
    /// <example>
    /// <code>
    /// routerBuilder.AddSegmentParser("user", static async (SegmentParserContext context) =>
    /// {
    ///     if (!Guid.TryParse(context.Segment, out Guid userId))
    ///         return new SegmentParseResult(false, null);
    ///
    ///     object? user = await context
    ///         .Services
    ///         .GetRequiredService&lt;IUserRepository&gt;()
    ///         .TryGetAsync(userId, context.Cancellation);
    ///
    ///     return new SegmentParseResult(user is not null, user);
    /// });
    /// </code>
    /// </example>
    public delegate ValueTask<SegmentParseResult> SegmentParserDelegate(SegmentParserContext context);

    /// <summary>
    /// Invokes the next compatible handler in the current routing pipeline.
    /// </summary>
    /// <returns>
    /// The <see cref="HttpResponseMessage"/> produced by the next matching handler.
    /// </returns>
    /// <remarks>
    /// This delegate is passed into <see cref="RequestHandlerDelegate"/> so handlers can opt into middleware-style
    /// composition. When the current handler does not call it, the pipeline stops at the current handler. Matching
    /// continues only within the route branch already selected for the request; sibling branches are not revisited.
    /// </remarks>
    /// <example>
    /// <code>
    /// routerBuilder.AddHandler("GET", "/api/users/{id:int}", async (requestContext, callNext) =>
    /// {
    ///     requestContext.Parameters["StartTime"] = DateTimeOffset.UtcNow;
    ///     return await callNext();
    /// });
    /// </code>
    /// </example>
    public delegate Task<HttpResponseMessage> CallNextHandlerDelegate();

    /// <summary>
    /// Represents a request handler in the router pipeline.
    /// </summary>
    /// <param name="requestContext">The current request context, including parsed route parameters and services.</param>
    /// <param name="callNext">A delegate that invokes the next compatible handler in the pipeline.</param>
    /// <returns>
    /// The response produced by the current handler, or by a later handler when <paramref name="callNext"/> is invoked.
    /// </returns>
    /// <remarks>
    /// Handlers may signal HTTP failures by calling <c>HttpRequestException.Throw(...)</c>. When
    /// <see cref="NanoRouteJsonExtensions.AddJsonErrorDetails{TBuilder}(TBuilder, bool)"/>, or equivalent custom
    /// middleware is registered, those exceptions can be translated into structured error responses. Throwing other
    /// exception types is also supported, but they are treated as unexpected failures:
    /// <see cref="NanoRouteExceptionExtensions.AddExceptionHandler{TBuilder}(TBuilder)"/> converts them into internal
    /// server error responses, while without such middleware they propagate to the caller unchanged.
    /// <see cref="OperationCanceledException"/> is left untouched so caller-driven cancellation and router timeouts
    /// can propagate to the transport layer or hosting code.
    /// </remarks>
    /// <example>
    /// <code>
    /// routerBuilder.AddHandler("GET", "/api/users/{user_id:int}/", (requestContext, callNext) =&gt;
    /// {
    ///     requestContext.Parameters["User"] = LoadUser((int) requestContext.Parameters["user_id"]!);
    ///     return callNext();
    /// });
    /// </code>
    /// </example>
    public delegate Task<HttpResponseMessage> RequestHandlerDelegate(RequestContext requestContext, CallNextHandlerDelegate callNext);
}
