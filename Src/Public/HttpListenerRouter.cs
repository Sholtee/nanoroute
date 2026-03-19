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
    /// 
    /// </summary>
    public class HttpListenerRouter : Router
    {
        // https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistenerresponse.headers?view=net-10.0#remarks
        private static readonly HashSet<string> s_reservedHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Content-Length",
            "Transfer-Encoding",
            "Keep-Alive",
            "Server"
        };

        private static async Task HandleResponse(HttpResponseMessage responseMessage, HttpListenerResponse response)
        {
            response.StatusCode = (int) responseMessage.StatusCode;

            CopyResponseHeaders(responseMessage.Headers);

            if (responseMessage.Content is not null)
            {
                CopyResponseHeaders(responseMessage.Content.Headers);

                using Stream buffer = await responseMessage.Content.ReadAsStreamAsync();
                await buffer.CopyToAsync(response.OutputStream);
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

            requestMessage.Properties["OriginalRequest"] = request;

            foreach (string header in request.Headers.AllKeys)
            {
                string[] values = request.Headers.GetValues(header);

                bool headerSet =
                    !requestMessage.Headers.TryAddWithoutValidation(header, values) || // this may return false for content headers like Content-Type
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header, values) is not true; // fall back to content headers

                if (!headerSet)
                    RouterEventSource.Log.Warn("HeaderCopyFailed", () => new
                    {
                        Header = header
                    });
            }

            return requestMessage;
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="context"></param>
        /// <param name="services"></param>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        public async Task Route(HttpListenerContext context, IServiceProvider services, CancellationToken cancellation = default)
        {
            Ensure.NotNull(context);
            Ensure.NotNull(services);

            HttpResponseMessage response;
            try
            {
                response = await Handle(GetRequest(context.Request), services, cancellation);
            }
            catch (OperationCanceledException)
            {
                context.Response.Abort();
                return;
            }

            await HandleResponse(response, context.Response);
        }

        /// <summary>
        /// 
        /// </summary>
        public static RouterBuilder<HttpListenerRouter> CreateBuilder() => new(static _ => { });
    }
}
