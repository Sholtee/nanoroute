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
            .AddEndpoint("GET", "/health/", endpoint => endpoint
                .WithHandler(static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("healthy", Encoding.UTF8, "text/plain")
                })))
            .AddEndpoint("GET", "/items/{id:int(min=1)}/", endpoint => endpoint
                .WithHandler(static (context, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"item:{context.Parameters["id"]}", Encoding.UTF8, "text/plain")
                })));

        private static async Task AssertBasicRoutes(HttpListenerRouter router)
        {
            await AssertPlainTextResponse(router, HttpMethod.Get, "health", HttpStatusCode.OK, "healthy").ConfigureAwait(false);
            await AssertPlainTextResponse(router, HttpMethod.Get, "items/42", HttpStatusCode.OK, "item:42").ConfigureAwait(false);
        }

        private static async Task AssertInMemoryRoutes()
        {
            HttpMessageRouter router = HttpMessageRouter
                .CreateBuilder()
                .AddEndpoint("GET", "/in-memory/", endpoint => endpoint
                    .WithHandler(static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("in-memory", Encoding.UTF8, "text/plain")
                    })))
                .CreateRouter();

            using HttpRequestMessage request = new(HttpMethod.Get, "https://example.test/in-memory");
            using HttpResponseMessage response = await router.Route(request, NullServiceProvider.Instance).ConfigureAwait(false);

            AssertEqual(HttpStatusCode.OK, response.StatusCode, "Unexpected status for in-memory route");
            AssertEqual("in-memory", await response.Content.ReadAsStringAsync().ConfigureAwait(false), "Unexpected body for in-memory route");
        }

        private static async Task AssertPlainTextResponse(HttpListenerRouter router, HttpMethod method, string relativeUri, HttpStatusCode expectedStatus, string expectedBody)
        {
            ResponseSnapshot response = await SendAsync(router, method, relativeUri).ConfigureAwait(false);

            AssertEqual(expectedStatus, response.StatusCode, $"Unexpected status for {method} {relativeUri}");
            AssertEqual(expectedBody, response.Body, $"Unexpected body for {method} {relativeUri}");
        }
    }
}
