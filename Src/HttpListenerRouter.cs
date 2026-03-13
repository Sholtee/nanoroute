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
    /// 
    /// </summary>
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
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="services"></param>
        /// <returns></returns>
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
