/********************************************************************************
* HttpListenerRouter.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute
{
    using Internals;

    /// <summary>
    /// Routes <see cref="HttpListenerContext"/> instances through a NanoRoute pipeline.
    /// </summary>
    /// <remarks>
    /// This adapter converts incoming <see cref="HttpListener"/> traffic into the core
    /// <see cref="HttpRequestMessage"/>/<see cref="HttpResponseMessage"/> pipeline used by NanoRoute.
    /// </remarks>
    /// <example>
    /// <code>
    /// HttpListenerRouter router = HttpListenerRouter
    ///     .CreateBuilder()
    ///     .AddDefaultValueParsers()
    ///     .AddHandler("GET", "/health/", (context, _) =&gt; Results.Ok())
    ///     .CreateRouter();
    /// </code>
    /// </example>
    public sealed class HttpListenerRouter
    {
        #region Private
        // https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistenerresponse.headers?view=net-10.0#remarks
        private static readonly FrozenSet<string> s_reservedHeaders = new List<string>
        {
            "Content-Length",
            "Transfer-Encoding",
            "Keep-Alive",
            "Server"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        private readonly RequestPipeline _pipeline;

        private HttpListenerRouter(RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> builder)
        {
            Config = builder.RouterConfig;
            _pipeline = new RequestPipeline(builder, Config.MatchingPrecedence);
        }

        private static async Task HandleResponse(HttpResponseMessage responseMessage, HttpListenerResponse response, CancellationToken cancellation)
        {
            response.StatusCode = (int) responseMessage.StatusCode;

            CopyResponseHeaders(responseMessage.Headers);

            if (responseMessage.Content is not null)
            {
                CopyResponseHeaders(responseMessage.Content.Headers);

                using Stream buffer = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);

                // https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/Stream.cs#L126
                await buffer.CopyToAsync(response.OutputStream, 81920, cancellation).ConfigureAwait(false);
            }

            response.Close();

            void CopyResponseHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
                    if (!s_reservedHeaders.Contains(header.Key))
                        response.Headers.Add(header.Key, string.Join(",", header.Value));
            }
        }

        private static HttpRequestMessage GetRequest(HttpListenerRequest request)
        {
            HttpRequestMessage requestMessage = new
            (
                new HttpMethod(request.HttpMethod),
                request.Url
            );

            if (request.HasEntityBody)
                requestMessage.Content = new StreamContent(request.InputStream);

            foreach (string headerName in request.Headers.AllKeys)
            {
                HttpHeaders headers = requestMessage.Content is not null && HttpRequestMessage.ContentHeaders.Contains(headerName)
                    ? requestMessage.Content.Headers
                    : requestMessage.Headers;

                // Some header (like Content-Type) has its default value. Without this line we'd just append the value list
                headers.Remove(headerName);
                headers.TryAddWithoutValidation(headerName, request.Headers.GetValues(headerName));
            }

            requestMessage.OriginalRequest = request;
            requestMessage.TraceId = request.RequestTraceIdentifier.ToString("N");

            return requestMessage;
        }
        #endregion

        /// <summary>
        /// Creates a strongly typed builder for <see cref="HttpListenerRouter"/>.
        /// </summary>
        /// <returns>A builder that can register handlers, value parsers, and router configuration.</returns>
        /// <example>
        /// <code>
        /// RouterBuilder&lt;HttpListenerRouter, HttpListenerRouterConfig&gt; builder = HttpListenerRouter.CreateBuilder();
        /// </code>
        /// </example>
        public static RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> CreateBuilder() => new(static builder => new HttpListenerRouter(builder));

        /// <summary>
        /// Configuration assigned to this instance.
        /// </summary>
        public HttpListenerRouterConfig Config { get; }

        /// <summary>
        /// Routes a single <see cref="HttpListener"/> request and writes the produced response.
        /// </summary>
        /// <param name="context">The listener context that supplies the request and receives the response.</param>
        /// <param name="services">The service provider exposed to handlers through <see cref="RequestContext.Services"/>.</param>
        /// <param name="cancellation">A token that can cancel request processing and response streaming.</param>
        /// <returns>A task that completes after the router has finished writing the response.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="context"/> or <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when the request uses an unsupported HTTP method.</exception>
        /// <exception cref="HttpRequestException">
        /// Thrown when no handler matches the request path or a matched handler signals an HTTP failure that is not
        /// translated by middleware.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the caller cancels <paramref name="cancellation"/>. The listener response is aborted before the
        /// exception is rethrown.
        /// </exception>
        /// <remarks>
        /// Request and content headers are copied into the intermediate <see cref="HttpRequestMessage"/>.
        /// The original <see cref="HttpListenerRequest"/> is available through the generated request message's
        /// <c>OriginalRequest</c> extension property.
        /// Response headers are copied back except for reserved <see cref="HttpListenerResponse"/> headers that
        /// must be managed by <see cref="HttpListener"/> itself. Cancellation is not translated into an HTTP error
        /// response by this adapter.
        /// </remarks>
        /// <example>
        /// <code>
        /// HttpListenerRouter router = HttpListenerRouter
        ///     .CreateBuilder()
        ///     .AddJsonErrorDetails()
        ///     .AddDefaultValueParsers()
        ///     .AddHandler("GET", "/hello/{name:str}/", (context, _) =&gt;
        ///         Task.FromResult(HttpResponseMessage.Json(new
        ///         {
        ///             message = $"Hello {context.Parameters["name"]}"
        ///         })))
        ///     .CreateRouter();
        ///
        /// await router.Route(listenerContext, services, cancellationToken);
        /// </code>
        /// </example>
        public async Task Route(HttpListenerContext context, IServiceProvider services, CancellationToken cancellation = default)
        {
            Ensure.NotNull(context);
            Ensure.NotNull(services);

            using HttpRequestMessage request = GetRequest(context.Request);

            try
            {
                using HttpResponseMessage response = await _pipeline.ExecuteAsync(request, services, cancellation).ConfigureAwait(false);

                await HandleResponse(response, context.Response, cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                context.Response.Abort();
                throw;
            }
        }
    }
}
