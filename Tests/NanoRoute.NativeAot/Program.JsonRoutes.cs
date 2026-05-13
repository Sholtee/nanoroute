/********************************************************************************
* Program.JsonRoutes.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NanoRoute.NativeAot
{
    internal static partial class Program
    {
        private static void ConfigureJsonRoutes(RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> builder) => builder
            .AddJsonBody("POST", "/items", AppJsonContext.Default.CreateItemRequest, "body")
            .AddHandler("POST", "/items", static (context, _) =>
            {
                CreateItemRequest body = (CreateItemRequest) context.Parameters["body"]!;
                return Task.FromResult(HttpResponseMessage.Json
                (
                    HttpStatusCode.Created,
                    new CreateItemResponse(body.Name, body.Quantity),
                    AppJsonContext.Default.CreateItemResponse
                ));
            });

        private static async Task AssertJsonRoutes(HttpListenerRouter router)
        {
            using StringContent content = new("{\"name\":\"widget\",\"quantity\":3}", Encoding.UTF8, "application/json");
            ResponseSnapshot response = await SendAsync(router, HttpMethod.Post, "items", content).ConfigureAwait(false);

            AssertEqual(HttpStatusCode.Created, response.StatusCode, "Unexpected status for POST /items");

            CreateItemResponse? body = JsonSerializer.Deserialize(response.Body, AppJsonContext.Default.CreateItemResponse);

            if (body is null)
                throw new InvalidOperationException("The JSON response body was null.");

            AssertEqual("widget", body.Name, "Unexpected JSON response name.");
            AssertEqual(3, body.Quantity, "Unexpected JSON response quantity.");
        }

        private static void ConfigureErrorResponses(RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> builder) => builder.AddJsonErrorDetails();

        private static async Task AssertNotFoundResponse(HttpListenerRouter router)
        {
            ResponseSnapshot response = await SendAsync(router, HttpMethod.Get, "missing").ConfigureAwait(false);

            AssertEqual(HttpStatusCode.NotFound, response.StatusCode, "Unexpected status for GET /missing");
            AssertEqual("application/json", response.MediaType, "Unexpected content type for GET /missing");

            ErrorDetails? body = JsonSerializer.Deserialize(response.Body, AppJsonContext.Default.ErrorDetails);

            if (body is null)
                throw new InvalidOperationException("The JSON error body was null.");

            AssertEqual(HttpStatusCode.NotFound, body.Status, "Unexpected error status.");
            AssertEqual(false, string.IsNullOrWhiteSpace(body.TraceId), "Expected a trace identifier in the error response.");
        }
    }

    internal sealed record CreateItemRequest(string Name, int Quantity);

    internal sealed record CreateItemResponse(string Name, int Quantity);

    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    [JsonSerializable(typeof(CreateItemRequest))]
    [JsonSerializable(typeof(CreateItemResponse))]
    [JsonSerializable(typeof(ErrorDetails))]
    internal sealed partial class AppJsonContext : JsonSerializerContext
    {
    }
}
