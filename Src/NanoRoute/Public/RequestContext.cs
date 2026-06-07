/********************************************************************************
* RequestContext.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

namespace NanoRoute
{
    /// <summary>
    /// Carries request-specific data through the NanoRoute handler pipeline.
    /// </summary>
    /// <remarks>
    /// Handlers can use <see cref="Parameters"/> to share values, <see cref="RemainingPath"/> to inspect the
    /// unmatched path tail, <see cref="Services"/> to resolve dependencies, and <see cref="Cancellation"/> to
    /// observe caller-initiated cancellation.
    /// <see cref="Parameters"/> is a shared mutable dictionary for the active pipeline so handlers may overwrite values
    /// that were written earlier by other handlers.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.AddHandler("GET", "/users/{id:int}/", (context, _) =&gt;
    /// {
    ///     int id = (int) context.Parameters["id"]!;
    ///     return LoadUserResponse(id, context.Services, context.Cancellation);
    /// });
    /// </code>
    /// </example>
    public readonly struct RequestContext
    {
        /// <summary>
        /// Gets the parsed route, query, and handler-shared values for the active request.
        /// </summary>
        /// <example>
        /// <code>
        /// object? id = requestContext.Parameters["id"];
        /// </code>
        /// </example>
        public required IDictionary<string, object?> Parameters { get; init; }

        /// <summary>
        /// Gets the service provider available to handlers and parsers.
        /// </summary>
        /// <example>
        /// <code>
        /// IUserRepository users = requestContext.Services.GetRequiredService&lt;IUserRepository&gt;();
        /// </code>
        /// </example>
        public required IServiceProvider Services { get; init; }

        /// <summary>
        /// Gets the request being routed.
        /// </summary>
        /// <example>
        /// <code>
        /// Uri? uri = requestContext.Request.RequestUri;
        /// </code>
        /// </example>
        public required HttpRequestMessage Request { get; init; }

        /// <summary>
        /// Gets the request path portion that has not been consumed by the current route match.
        /// </summary>
        /// <remarks>
        /// The value comes from <see cref="Uri.AbsolutePath"/> and does not include the query string. Prefix
        /// handlers receive the unmatched tail with the leading slash before the next segment, such as
        /// <c>/details</c>. Exact handlers receive an empty value when no path remains.
        /// </remarks>
        /// <example>
        /// <code>
        /// string tail = requestContext.RemainingPath.ToString();
        /// </code>
        /// </example>
        public required ReadOnlyMemory<char> RemainingPath { get; init; }

        /// <summary>
        /// Gets the cancellation token for the active request.
        /// </summary>
        /// <example>
        /// <code>
        /// requestContext.Cancellation.ThrowIfCancellationRequested();
        /// </code>
        /// </example>
        public CancellationToken Cancellation { get; init; }
    }
}
