/********************************************************************************
* Program.BasicRoutes.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NanoRoute.NativeAot
{
    internal static partial class Program
    {
        private static void ConfigureBasicRoutes(RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> builder) => builder
            .AddIntParser()
            .AddHandler("GET", "/health", static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("healthy", Encoding.UTF8, "text/plain")
            }))
            .AddHandler("GET", "/items/{id:int(min=1)}", static (context, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"item:{context.Parameters["id"]}", Encoding.UTF8, "text/plain")
            }));

        private static async Task AssertBasicRoutes(HttpListenerRouter router)
        {
            await AssertPlainTextResponse(router, HttpMethod.Get, "health", HttpStatusCode.OK, "healthy").ConfigureAwait(false);
            await AssertPlainTextResponse(router, HttpMethod.Get, "items/42", HttpStatusCode.OK, "item:42").ConfigureAwait(false);
        }

        private static async Task AssertPlainTextResponse(HttpListenerRouter router, HttpMethod method, string relativeUri, HttpStatusCode expectedStatus, string expectedBody)
        {
            ResponseSnapshot response = await SendAsync(router, method, relativeUri).ConfigureAwait(false);

            AssertEqual(expectedStatus, response.StatusCode, $"Unexpected status for {method} {relativeUri}");
            AssertEqual(expectedBody, response.Body, $"Unexpected body for {method} {relativeUri}");
        }
    }
}
