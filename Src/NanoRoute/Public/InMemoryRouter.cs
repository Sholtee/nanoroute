/********************************************************************************
* InMemoryRouter.cs                                                             *
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
    /// Routes <see cref="HttpRequestMessage"/> instances through a NanoRoute pipeline without a transport adapter.
    /// </summary>
    /// <remarks>
    /// Use this router when the request is already represented as an <see cref="HttpRequestMessage"/>, such as
    /// in unit tests, in-memory integrations, or custom hosting code. The caller owns disposal of the request and
    /// the returned response.
    /// </remarks>
    /// <example>
    /// <code>
    /// InMemoryRouter router = InMemoryRouter
    ///     .CreateBuilder()
    ///     .AddDefaultValueParsers()
    ///     .AddHandler("GET", "/hello/{name:str}/", static (context, _) =&gt;
    ///         Task.FromResult(HttpResponseMessage.Json(HttpStatusCode.OK, new
    ///         {
    ///             message = $"Hello {context.Parameters["name"]}"
    ///         })))
    ///     .CreateRouter();
    ///
    /// using HttpResponseMessage response = await router.Route(request, services, cancellationToken);
    /// </code>
    /// </example>
    public sealed class InMemoryRouter
    {
        private readonly RequestPipeline _pipeline;

        private InMemoryRouter(RouterBuilder<InMemoryRouter, RouterConfig> builder)
        {
            Config = builder.RouterConfig;
            _pipeline = new RequestPipeline(builder, Config.MatchingPrecedence);
        }

        /// <summary>
        /// Creates a strongly typed builder for <see cref="InMemoryRouter"/>.
        /// </summary>
        /// <returns>A builder that can register handlers, value parsers, and router configuration.</returns>
        /// <example>
        /// <code>
        /// RouterBuilder&lt;InMemoryRouter, RouterConfig&gt; builder = InMemoryRouter.CreateBuilder();
        /// </code>
        /// </example>
        public static RouterBuilder<InMemoryRouter, RouterConfig> CreateBuilder() => new(static builder => new InMemoryRouter(builder));

        /// <summary>
        /// Configuration assigned to this instance.
        /// </summary>
        public RouterConfig Config { get; }

        /// <summary>
        /// Routes a single in-memory request and returns the produced response.
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
        /// <example>
        /// <code>
        /// using HttpRequestMessage request = new(HttpMethod.Get, "https://example.test/health");
        /// using HttpResponseMessage response = await router.Route(request, services, cancellationToken);
        /// </code>
        /// </example>
        public Task<HttpResponseMessage> Route(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation = default)
        {
            Ensure.NotNull(request);
            Ensure.NotNull(services);

            return _pipeline.ExecuteAsync(request, services, cancellation);
        }
    }
}
