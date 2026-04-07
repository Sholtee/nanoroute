/********************************************************************************
* HttpListenerRouter.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
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
    public class HttpListenerRouter: Router
    {
        // https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistenerresponse.headers?view=net-10.0#remarks
        private static readonly HashSet<string> s_reservedHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Content-Length",
            "Transfer-Encoding",
            "Keep-Alive",
            "Server"
        };

        private static async Task HandleResponse(HttpResponseMessage responseMessage, HttpListenerResponse response, CancellationToken cancellation)
        {
            response.StatusCode = (int) responseMessage.StatusCode;

            CopyResponseHeaders(responseMessage.Headers);

            if (responseMessage.Content is not null)
            {
                CopyResponseHeaders(responseMessage.Content.Headers);

                using Stream buffer = await responseMessage.Content.ReadAsStreamAsync();

                // https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/Stream.cs#L126
                await buffer.CopyToAsync(response.OutputStream, 81920, cancellation);
            }

            response.Close();

            void CopyResponseHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
            {
                cancellation.ThrowIfCancellationRequested();

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

            requestMessage.Properties[ORIGINAL_REQUEST_NAME] = request;
            requestMessage.Properties[TRACE_ID_NAME] = request.RequestTraceIdentifier.ToString("N");

            foreach (string header in request.Headers.AllKeys)
            {
                string[] values = request.Headers.GetValues(header);

                bool headerSet =
                    requestMessage.Headers.TryAddWithoutValidation(header, values) || // normal request headers
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header, values) is true; // fall back to content headers

                if (!headerSet)
                    RouterEventSource.Log.Warn("HeaderCopyFailed", () => new
                    {
                        Header = header
                    });
            }

            return requestMessage;
        }

        private HttpListenerRouter(RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> builder) : base(builder, builder.RouterConfig) { }

        /// <summary>
        /// Routes a single <see cref="HttpListener"/> request and writes the produced response.
        /// </summary>
        /// <param name="context">The listener context that supplies the request and receives the response.</param>
        /// <param name="services">The service provider exposed to handlers through <see cref="RequestContext.Services"/>.</param>
        /// <param name="cancellation">A token that can cancel request processing and response streaming.</param>
        /// <returns>A task that completes after the router has finished writing the response.</returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the caller cancels <paramref name="cancellation"/> or when the configured router timeout
        /// elapses. In either case the listener response is aborted before the exception is rethrown.
        /// </exception>
        /// <remarks>
        /// Request and content headers are copied into the intermediate <see cref="HttpRequestMessage"/>.
        /// The original <see cref="HttpListenerRequest"/> is stored in
        /// <see cref="Router.ORIGINAL_REQUEST_NAME"/> on the generated request message.
        /// Response headers are copied back except for reserved <see cref="HttpListenerResponse"/> headers that
        /// must be managed by <see cref="HttpListener"/> itself. Cancellation is not translated into an HTTP error
        /// response by this adapter.
        /// </remarks>
        /// <example>
        /// <code>
        /// HttpListenerRouter router = HttpListenerRouter
        ///     .CreateBuilder()
        ///     .AddJsonErrorDetails()
        ///     .AddDefaultParsers()
        ///     .AddHandler("GET", "/hello/{name:str}", (context, _) =&gt;
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
                using HttpResponseMessage response = await Handle(request, services, cancellation);

                await HandleResponse(response, context.Response, cancellation);
            }
            catch (OperationCanceledException)
            {
                context.Response.Abort();
                throw;
            }
        }

        /// <summary>
        /// Creates a strongly typed builder for configuring an <see cref="HttpListenerRouter"/>.
        /// </summary>
        /// <returns>A builder that can register handlers, segment parsers, and router configuration.</returns>
        public static RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> CreateBuilder() => new(static bldr => new HttpListenerRouter(bldr));
    }
}