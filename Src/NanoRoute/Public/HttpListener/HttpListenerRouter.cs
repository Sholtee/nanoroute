/********************************************************************************
* HttpListenerRouter.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
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
    public sealed class HttpListenerRouter : RouterBase<HttpListenerRouterConfig>
    {
        #region Private
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly FrozenSet<string> s_managedResponseHeaders = new List<string>
        {
            "Content-Length",
            "Content-Type",
            "Transfer-Encoding",
            "Keep-Alive",
            "Server",
            "WWW-Authenticate"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        private HttpListenerRouter(RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> builder) : base(builder, builder.RouterConfig)
        {
        }

        internal static async Task HandleResponse(HttpResponseMessage responseMessage, HttpListenerResponse response, CancellationToken cancellation)
        {
            response.StatusCode = (int) responseMessage.StatusCode;

            CopyResponseHeaders(responseMessage.Headers, response.Headers);

            if (responseMessage.Content is not null)
            {
                if (responseMessage.Content.Headers.ContentType is { } contentType)
                    response.ContentType = contentType.ToString();

                CopyResponseHeaders(responseMessage.Content.Headers, response.Headers);

                using Stream contentStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);

                if (contentStream.CanSeek)
                {
                    long contentLength = contentStream.Length - contentStream.Position;
                    if (contentLength >= 0)
                        response.ContentLength64 = contentLength;
                }

                // https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/Stream.cs#L126
                await contentStream.CopyToAsync(response.OutputStream, 81920, cancellation).ConfigureAwait(false);
            }

            response.Close();

            static void CopyResponseHeaders(HttpHeaders headers, WebHeaderCollection target)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
                    if (!s_managedResponseHeaders.Contains(header.Key))
                        foreach (string value in header.Value)
                            target.Add(header.Key, value);
            }
        }

        internal static HttpRequestMessage GetRequest(HttpListenerRequest request)
        {
            HttpRequestMessage requestMessage = new
            (
                HttpMethod.For(request.HttpMethod),
                request.Url
            );

            if (request.HasEntityBody)
            {
                requestMessage.Content = new StreamContent(request.InputStream);
                requestMessage.Content.Headers.Clear();
            }

            HttpHeaders
                requestHeaders = requestMessage.Headers,
                contentHeaders = requestMessage.Content?.Headers!;

            for (int i = 0; i < request.Headers.Count; i++)
            {
                string? headerName = request.Headers.GetKey(i);
                if (headerName is null)
                    continue;

                HttpHeaders headers = contentHeaders is not null && HttpRequestMessage.ContentHeaders.Contains(headerName)
                    ? contentHeaders
                    : requestHeaders;

                headers.TryAddWithoutValidation(headerName, request.Headers.GetValues(i));
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
        /// Routes a single <see cref="HttpListener"/> request and writes the produced response.
        /// </summary>
        /// <param name="context">The listener context that supplies the request and receives the response.</param>
        /// <param name="services">The service provider exposed to handlers through <see cref="RequestContext.Services"/>.</param>
        /// <param name="cancellation">A token that can cancel request processing and response streaming.</param>
        /// <returns>A task that completes after the router has finished writing the response.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="context"/> or <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown when the request uses an unsupported HTTP method.</exception>
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
                using HttpResponseMessage response = await Route(request, services, cancellation).ConfigureAwait(false);

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
