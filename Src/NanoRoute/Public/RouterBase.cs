/********************************************************************************
* RouterBase.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute
{
    using Internals;

    /// <summary>
    /// Provides shared configuration storage and request-pipeline execution for transport-specific router adapters.
    /// </summary>
    /// <typeparam name="TConfig">The immutable router configuration type used by the adapter.</typeparam>
    /// <remarks>
    /// Derive from this type when a custom transport needs to translate its native request model into an
    /// <see cref="HttpRequestMessage"/> while reusing NanoRoute's matching, value parsing, middleware, and handler
    /// pipeline. The constructor captures an immutable route snapshot, and derived adapters can call
    /// <see cref="Route(HttpRequestMessage, IServiceProvider, CancellationToken)"/> from their public entry point.
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed class MyRouter : RouterBase&lt;RouterConfig&gt;
    /// {
    ///     private MyRouter(RouteScopeBuilder routes, RouterConfig config)
    ///         : base(routes, config)
    ///     {
    ///     }
    ///
    ///     public async Task&lt;HttpResponseMessage&gt; Route(MyRequest request, IServiceProvider services, CancellationToken cancellation)
    ///     {
    ///         using HttpRequestMessage httpRequest = request.ToHttpRequestMessage();
    ///         return await Route(httpRequest, services, cancellation).ConfigureAwait(false);
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class RouterBase<TConfig> where TConfig : RouterConfig
    {
        private readonly RequestPipeline _pipeline;

        /// <summary>
        /// Initializes a router from the current route scope and configuration.
        /// </summary>
        /// <param name="routes">The route scope whose registered routes are captured by the router.</param>
        /// <param name="config">The configuration assigned to the router.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="routes"/> or <paramref name="config"/> is <see langword="null"/>.
        /// </exception>
        protected RouterBase(RouteScopeBuilder routes, TConfig config)
        {
            Ensure.NotNull(routes);
            Ensure.NotNull(config);

            Config = config;
            _pipeline = new RequestPipeline(routes.CreateSnapshot(), config.MatchingPrecedence);
        }

        /// <summary>
        /// Gets the configuration assigned to this router instance.
        /// </summary>
        public TConfig Config { get; }

        /// <summary>
        /// Routes a single HTTP request message and returns the produced response.
        /// </summary>
        /// <param name="request">The request to process.</param>
        /// <param name="services">The service provider exposed to handlers through <see cref="RequestContext.Services"/>.</param>
        /// <param name="cancellation">A token that can cancel request processing.</param>
        /// <returns>The response produced by the matching handlers.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="request"/> or <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown when the request uses an unsupported HTTP method.</exception>
        /// <exception cref="HttpRequestException">
        /// Thrown when no handler matches the request path or a matched handler signals an HTTP failure that is not
        /// translated by middleware.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the caller cancels <paramref name="cancellation"/>.
        /// </exception>
        /// <remarks>
        /// The returned <see cref="HttpResponseMessage"/> is not disposed by the router. Callers should dispose it
        /// after reading the response body.
        /// </remarks>
        protected Task<HttpResponseMessage> Route(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation)
        {
            Ensure.NotNull(request);
            Ensure.NotNull(services);

            return _pipeline.ExecuteAsync(request, services, cancellation);
        }
    }
}
