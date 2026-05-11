/********************************************************************************
* Program.TypedHandlers.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.NativeAot
{
    internal static partial class Program
    {
        private static void ConfigureTypedHandlerRoutes(RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> builder) => builder
            .AddStringParser()
            .AddQueryBindings("GET", "/typed/items/{id:int(min=1)}", "{query_filter:str(min=3)}")
            .AddHandler(["GET"], "/typed/items/{id:int(min=1)}", static (TypedRequest request) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{request.Service.Prefix}:{request.Id}:{request.Filter}", Encoding.UTF8, "text/plain")
            }))
            .AddHandler(["GET"], "/typed/middleware/{id:int(min=1)}", static async (TypedMiddlewareRequest request, CallNextHandlerDelegate next) =>
            {
                HttpResponseMessage response = await next().ConfigureAwait(false);
                response.Headers.Add("X-Typed-Id", request.Id.ToString());
                return response;
            })
            .AddHandler("GET", "/typed/middleware/{id:int(min=1)}", static (context, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"middleware:{context.Parameters["id"]}", Encoding.UTF8, "text/plain")
            }));

        private static async Task AssertTypedHandlerRoutes(HttpListenerRouter router)
        {
            await AssertPlainTextResponse(router, HttpMethod.Get, "typed/items/42?query_filter=spikey", HttpStatusCode.OK, "typed:42:spikey").ConfigureAwait(false);
            await AssertTypedMiddlewareResponse(router).ConfigureAwait(false);
        }

        private static async Task AssertTypedMiddlewareResponse(HttpListenerRouter router)
        {
            ResponseSnapshot response = await SendAsync(router, HttpMethod.Get, "typed/middleware/42").ConfigureAwait(false);

            AssertEqual(HttpStatusCode.OK, response.StatusCode, "Unexpected status for GET /typed/middleware/42");
            AssertEqual("middleware:42", response.Body, "Unexpected body for GET /typed/middleware/42");
            AssertEqual("42", response.TypedIdHeader, "Unexpected middleware header for GET /typed/middleware/42");
        }
    }

    internal sealed record GreetingService(string Prefix);

    internal sealed class TypedRequest
    {
        public int Id { get; set; }

        [ValueSource(ValueSource.Context, Name = "query_filter")]
        public string Filter { get; set; } = null!;

        [ValueSource(ValueSource.ServiceLocator)]
        public GreetingService Service { get; set; } = null!;

        public CancellationToken Cancellation { get; set; }
    }

    internal sealed class TypedMiddlewareRequest
    {
        public int Id { get; set; }
    }
}
