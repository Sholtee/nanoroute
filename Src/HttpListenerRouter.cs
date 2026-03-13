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
using System.Threading.Tasks;

namespace NanoRoute
{
    /// <summary>
    /// Bridges <see cref="HttpListenerContext"/> requests to <see cref="HttpMessageRouter"/>.
    /// </summary>
    /// <remarks>
    /// This router translates the incoming <see cref="HttpListenerRequest"/> into an
    /// <see cref="HttpRequestMessage"/>, executes the registered NanoRoute pipeline, then copies the produced
    /// <see cref="HttpResponseMessage"/> back to <see cref="HttpListenerResponse"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// HttpListenerRouter router = new HttpListenerRouter();
    ///
    /// router
    ///     .AddDefaultHandler()
    ///     .AddHandler("GET", "/health", (context, next) =&gt;
    ///         Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    ///         {
    ///             Content = new StringContent("ok")
    ///         }));
    ///
    /// HttpListenerContext listenerContext = await listener.GetContextAsync();
    /// await router.Route(listenerContext, services);
    /// </code>
    /// </example>
    public class HttpListenerRouter : HttpMessageRouter
    {
        private const string ORIGINAL_REQUEST = "OriginalRequest";

        // https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistenerresponse.headers?view=net-10.0#remarks
        private static readonly HashSet<string> s_reservedHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Content-Length",
            "Transfer-Encoding",
            "Keep-Alive",
            "Server"
        };

        /// <summary>
        /// Routes a single <see cref="HttpListenerContext"/> through the configured handlers.
        /// </summary>
        /// <param name="context">The listener context containing the incoming request and outgoing response.</param>
        /// <param name="services">The service provider exposed through the created <see cref="RequestContext{TRequest}"/>.</param>
        /// <returns>A task that completes when the response has been written to the listener output stream.</returns>
        /// <remarks>
        /// Request headers are copied to the intermediate <see cref="HttpRequestMessage"/>, including content
        /// headers when the request has a body. Response headers from both <see cref="HttpResponseMessage.Headers"/>
        /// and <see cref="HttpResponseMessage.Content"/> are written back to the listener response, except for
        /// headers that <see cref="HttpListenerResponse"/> manages itself.
        /// </remarks>
        public async Task Route(HttpListenerContext context, IServiceProvider services)
        {
            Ensure.NotNull(context);
            Ensure.NotNull(services);

            HttpRequestMessage request = new
            (
                new HttpMethod(context.Request.HttpMethod),
                context.Request.Url
            );

            if (context.Request.HasEntityBody)
                request.Content = new StreamContent(context.Request.InputStream);

            foreach (string header in context.Request.Headers.AllKeys)
            {
                string[] values = context.Request.Headers.GetValues(header);

                bool headerSet =
                    !request.Headers.TryAddWithoutValidation(header, values) || // this may return false for content headers like Content-Type
                    request.Content?.Headers.TryAddWithoutValidation(header, values) is not true; // fall back to content headers

                if (!headerSet)
                    RouterEventSource.Log.Warn("HeaderCopyFailed", () => new
                    {
                        Header = header
                    });
            }
            
            request.Properties[ORIGINAL_REQUEST] = context.Request;
            
            HttpResponseMessage response = await Handle(request, services);

            context.Response.StatusCode = (int) response.StatusCode;

            CopyResponseHeaders(response.Headers);
            CopyResponseHeaders(response.Content.Headers);

            using Stream buffer = await response.Content.ReadAsStreamAsync();
            await buffer.CopyToAsync(context.Response.OutputStream);

            context.Response.Close();

            void CopyResponseHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
            {
                foreach(KeyValuePair<string, IEnumerable<string>> header in headers)
                    if (!s_reservedHeaders.Contains(header.Key))
                        context.Response.Headers.Add(header.Key, string.Join(",", header.Value));
            }
        }
    }
}
