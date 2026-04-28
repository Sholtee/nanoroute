/********************************************************************************
* Router.cs                                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// Executes the route matching pipeline built by <see cref="RouteBuilder"/>.
    /// </summary>
    /// <remarks>
    /// A router is created from a builder snapshot. Matching walks the configured route tree, attaches bound parameters, and invokes compatible
    /// handlers in order until one returns a response without delegating further.
    /// </remarks>
    public abstract class Router: RoutingContext
    {
        /// <summary>
        /// The request property key that stores the trace identifier associated with the current request.
        /// </summary>
        public const string TRACE_ID_NAME = "TraceId";

        /// <summary>
        /// The request property key that stores the original transport-specific request object.
        /// </summary>
        public const string ORIGINAL_REQUEST_NAME = "OriginalRequest";

        private static RouteNode CopyRoot(RouteBuilder routeBuilder)
        {
            // The base() ctor invocation runs first so we have to do the validation here
            Ensure.NotNull(routeBuilder);
            return routeBuilder.GetRoot(frozen: true);
        }

        private IDisposable? CreateLinkedTokenIfNecessary(ref CancellationToken cancellation)
        {
            if (Timeout.Equals(System.Threading.Timeout.InfiniteTimeSpan))
                return null!;

            if (cancellation.Equals(default))
            {
                CancellationTokenSource src = new(Timeout);
                cancellation = src.Token;
                return src;
            }

            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            linked.CancelAfter(Timeout);
            cancellation = linked.Token;
            return linked;
        }

        /// <summary>
        /// Initializes a router from a route builder snapshot and router configuration.
        /// </summary>
        /// <param name="routeBuilder">The builder whose current route tree should be frozen into this router.</param>
        /// <param name="config">The router configuration that controls runtime behavior.</param>
        [SuppressMessage("ApiDesign", "RS0022:Constructor make noninheritable base class inheritable")]
        protected Router(RouteBuilder routeBuilder, RouterConfig config): base(CopyRoot(routeBuilder))
        {
            //Ensure.NotNull(routeBuilder);
            Ensure.NotNull(config);

            MatchingBehavior = config.MatchingBehavior;
            Timeout = config.Timeout;
        }

        /// <summary>
        /// Gets the configured precedence between literal and parameterized child segments.
        /// </summary>
        public MatchingBehavior MatchingBehavior
        {
            get;
            private init
            {
                if (!Enum.IsDefined(typeof(MatchingBehavior), value))
                    throw new ArgumentOutOfRangeException(nameof(value));

                field = value;
            }
        }

        /// <summary>
        /// Gets the maximum time the router allows a request to remain in the matching and handler pipeline.
        /// </summary>
        /// <remarks>
        /// The effective pipeline token is canceled when either the caller-supplied cancellation token is canceled
        /// or this timeout elapses.
        /// </remarks>
        public TimeSpan Timeout { get; }

        /// <summary>
        /// Routes an <see cref="HttpRequestMessage"/> through the configured handler pipeline.
        /// </summary>
        /// <param name="request">The request to process.</param>
        /// <param name="services">The service provider exposed to value parsers and handlers.</param>
        /// <param name="cancellation">A token that can cancel request processing.</param>
        /// <returns>The <see cref="HttpResponseMessage"/> produced by the matching handlers.</returns>
        /// <remarks>
        /// Prefix routes can participate in the same pipeline as exact routes. Consecutive <c>/</c> separators in the
        /// request path are treated as a single separator during matching. When several handlers match, NanoRoute
        /// evaluates compatible matches from shorter prefixes toward more specific matches and honors
        /// <see cref="MatchingBehavior"/> when both literal and parameterized segments are available at the same depth.
        /// Once a branch is selected at a given depth, NanoRoute does not return to sibling branches later in the pipeline.
        /// </remarks>
        /// <exception cref="HttpRequestException">Thrown when no handler matches the request path.</exception>
        /// <exception cref="ArgumentException">Thrown when the request uses an unsupported HTTP method.</exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the caller cancels the <paramref name="cancellation"/> or when the configured <see cref="Timeout"/>
        /// elapses.
        /// </exception>
        #if DEBUG
        internal
        #endif
        protected async Task<HttpResponseMessage> Handle(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation = default)
        {
            Ensure.NotNull(request);
            Ensure.NotNull(services);

            RouterEventSource.Log.Info("RequestProcessingStarted", () => new
            {
                RequestUri = request.RequestUri.OriginalString,
                Verb = request.Method.Method
            });

            using IDisposable? cancellationSoke = CreateLinkedTokenIfNecessary(ref cancellation);

            await using RequestPipeline pipeline = new(_root, MatchingBehavior, request, services, cancellation);

            return await pipeline.RunAsync();
        }
    }
}
