/********************************************************************************
* RequestPipeline.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// Executes the route matching pipeline built by <see cref="RouteScopeBuilder"/>.
    /// </summary>
    /// <remarks>
    /// A request pipeline is created from a builder snapshot. Matching walks the configured route tree, attaches bound
    /// parameters, and invokes compatible handlers in order until one returns a response without delegating further.
    /// Use this type when implementing custom transport routers over the core
    /// <see cref="HttpRequestMessage"/>/<see cref="HttpResponseMessage"/> pipeline.
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed class MyRouter
    /// {
    ///     private readonly RequestPipeline _pipeline;
    ///
    ///     private MyRouter(RouterBuilder&lt;MyRouter, RouterConfig&gt; builder)
    ///     {
    ///         Config = builder.RouterConfig;
    ///         _pipeline = new RequestPipeline(builder, Config.MatchingPrecedence);
    ///     }
    ///
    ///     public RouterConfig Config { get; }
    ///
    ///     public static RouterBuilder&lt;MyRouter, RouterConfig&gt; CreateBuilder() =&gt;
    ///         new(static builder =&gt; new MyRouter(builder));
    /// }
    /// </code>
    /// </example>
    public sealed class RequestPipeline
    {
        private readonly RouteNode _root;

        /// <summary>
        /// Creates a new <see cref="RequestPipeline"/> instance.
        /// </summary>
        /// <param name="routeScopeBuilder">The builder scope whose registered routes are captured by the pipeline.</param>
        /// <param name="matchingPrecedence">How the pipeline prioritizes literal and parameterized child segments during matching.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="routeScopeBuilder"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="matchingPrecedence"/> is not a defined <see cref="MatchingPrecedence"/> value.
        /// </exception>
        public RequestPipeline(RouteScopeBuilder routeScopeBuilder, MatchingPrecedence matchingPrecedence)
        {
            Ensure.NotNull(routeScopeBuilder);
            if (!Enum.IsDefined(typeof(MatchingPrecedence), matchingPrecedence))
                throw new ArgumentOutOfRangeException(nameof(matchingPrecedence));

            _root = routeScopeBuilder.CreateSnapshot();
            MatchingPrecedence = matchingPrecedence;
        }

        internal RequestPipeline(RouteNode root, MatchingPrecedence matchingPrecedence)
        {
            Ensure.NotNull(root);
            if (!Enum.IsDefined(typeof(MatchingPrecedence), matchingPrecedence))
                throw new ArgumentOutOfRangeException(nameof(matchingPrecedence));

            _root = root;
            MatchingPrecedence = matchingPrecedence;
        }

        /// <summary>
        /// Executes an <see cref="HttpRequestMessage"/> through the configured route handler pipeline.
        /// </summary>
        /// <param name="request">The request to process.</param>
        /// <param name="services">The service provider exposed to value parsers and handlers.</param>
        /// <param name="cancellation">A token that can cancel request processing.</param>
        /// <returns>The <see cref="HttpResponseMessage"/> produced by the matching handlers.</returns>
        /// <remarks>
        /// Prefix routes can participate in the same pipeline as exact routes. Consecutive <c>/</c> separators in the
        /// request path are treated as a single separator during matching. When several handlers match, NanoRoute
        /// evaluates compatible matches from shorter prefixes toward more specific matches and honors
        /// <see cref="MatchingPrecedence"/> when both literal and parameterized segments are available at the same depth.
        /// Once a branch is selected at a given depth, NanoRoute does not return to sibling branches later in the pipeline.
        /// </remarks>
        /// <exception cref="HttpRequestException">Thrown when no handler matches the request path.</exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="request"/> or <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown when the request uses an unsupported HTTP method.</exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the caller cancels the <paramref name="cancellation"/>.
        /// </exception>
        /// <example>
        /// <code>
        /// using HttpResponseMessage response = await pipeline.ExecuteAsync(request, services, cancellation);
        /// </code>
        /// </example>
        public async Task<HttpResponseMessage> ExecuteAsync(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation = default)
        {
            Ensure.NotNull(request);
            Ensure.NotNull(services);

            if (!HttpVerb.TryParseFast(request.Method.Method, out HttpVerb verb))
                throw new InvalidOperationException
                (
                    string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, request.Method.Method)
                );

            RouterEventSource.Info.Write("RequestProcessingStarted", static request => new
            {
                RequestUri = request.RequestUri.OriginalString,
                Verb = request.Method.Method
            }, request);

            using RouteMatchCursor cursor = new
            (
                _root,
                verb,
                request.RequestUri,
                services,
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                MatchingPrecedence,
                cancellation
            );

            return await CallNextHandler().ConfigureAwait(false);

            async Task<HttpResponseMessage> CallNextHandler()
            {
                if (!await cursor.MoveNextAsync().ConfigureAwait(false))
                {
                    RouterEventSource.Info.Write("NoMatchingHandler", static request => new
                    {
                        RequestUri = request.RequestUri.OriginalString,
                        Verb = request.Method.Method
                    }, request);

                    HttpRequestException.Throw(HttpStatusCode.NotFound, Resources.ERR_NOT_FOUND);
                }

                RouterEventSource.Info.Write("MatchingHandler", static (request, cursor) => new
                {
                    RequestUri = request.RequestUri.OriginalString,
                    Verb = request.Method.Method,
                    cursor.HandlerRegistration.Pattern,
                    ParameterCount = cursor.Parameters.Count
                }, request, cursor);

                RequestContext requestContext = new()
                {
                    Parameters = cursor.Parameters,
                    RemainingPath = cursor.RemainingPath,
                    Services = services,
                    Request = request,
                    Cancellation = cancellation
                };

                return await cursor
                    .HandlerRegistration
                    .Handler(requestContext, CallNextHandler)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets how the pipeline prioritizes literal and parameterized child segments during matching.
        /// </summary>
        public MatchingPrecedence MatchingPrecedence { get; }
    }
}
