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
        private static readonly HashSet<HttpResponseHeader> s_reservedHeaders =
        [
            HttpResponseHeader.ContentLength,
            HttpResponseHeader.TransferEncoding,
            HttpResponseHeader.KeepAlive,
            HttpResponseHeader.WwwAuthenticate
        ];

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
                    request.Headers.TryAddWithoutValidation(header, values) || // this may return false for content headers like Content-Type
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

            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
                if (Enum.TryParse(header.Key, out HttpResponseHeader responseHeader))
                    if (!s_reservedHeaders.Contains(responseHeader))
                        context.Response.Headers.Add(responseHeader, string.Join(",", header.Value));

            using Stream buffer = await response.Content.ReadAsStreamAsync();
            await buffer.CopyToAsync(context.Response.OutputStream);

            context.Response.Close();
        }
    }
}
