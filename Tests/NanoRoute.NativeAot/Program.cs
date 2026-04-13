/********************************************************************************
* Program.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NanoRoute.NativeAot
{
    using Json;

    internal static class Program
    {
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Console smoke test exits with a non-zero code after writing the failure.")]
        private static async Task<int> Main()
        {
            try
            {
                HttpListenerRouter router = HttpListenerRouter
                    .CreateBuilder()
                    .AddIntParser()
                    .AddJsonErrorDetails()
                    .AddHandler("GET", "/health", static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("healthy", Encoding.UTF8, "text/plain")
                    }))
                    .AddHandler("GET", "/items/{id:int(min=1)}", static (context, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"item:{context.Parameters["id"]}", Encoding.UTF8, "text/plain")
                    }))
                    .AddJsonBody(AppJsonContext.Default.CreateItemRequest, "body", "/items", ["POST"])
                    .AddHandler("POST", "/items", static (context, _) =>
                    {
                        CreateItemRequest body = (CreateItemRequest) context.Parameters["body"]!;
                        return Task.FromResult(HttpResponseMessage.Json
                        (
                            HttpStatusCode.Created,
                            new CreateItemResponse(body.Name, body.Quantity),
                            AppJsonContext.Default.CreateItemResponse
                        ));
                    })
                    .CreateRouter();

                await AssertPlainTextResponse(router, HttpMethod.Get, "health", HttpStatusCode.OK, "healthy").ConfigureAwait(false);
                await AssertPlainTextResponse(router, HttpMethod.Get, "items/42", HttpStatusCode.OK, "item:42").ConfigureAwait(false);
                await AssertJsonResponse(router).ConfigureAwait(false);
                await AssertNotFoundResponse(router).ConfigureAwait(false);

                await Console.Out.WriteLineAsync("Native AOT smoke tests passed.").ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false);
                return 1;
            }
        }

        private static async Task AssertPlainTextResponse(HttpListenerRouter router, HttpMethod method, string relativeUri, HttpStatusCode expectedStatus, string expectedBody)
        {
            ResponseSnapshot response = await SendAsync(router, method, relativeUri).ConfigureAwait(false);

            AssertEqual(expectedStatus, response.StatusCode, $"Unexpected status for {method} {relativeUri}");
            AssertEqual(expectedBody, response.Body, $"Unexpected body for {method} {relativeUri}");
        }

        private static async Task AssertJsonResponse(HttpListenerRouter router)
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

        [SuppressMessage("Reliability", "CA2025:Ensure tasks using disposables complete before disposables are disposed", Justification = "The request/response lifetime is fully awaited inside this helper before any disposable leaves scope.")]
        private static async Task<ResponseSnapshot> SendAsync(HttpListenerRouter router, HttpMethod method, string relativeUri, HttpContent? content = null)
        {
            Uri baseAddress = new($"http://localhost:{GetFreePort()}/");

            using HttpListener listener = new();
            listener.Prefixes.Add(baseAddress.AbsoluteUri);
            listener.Start();

            using HttpClient client = new()
            {
                BaseAddress = baseAddress
            };

            using HttpRequestMessage request = new(method, relativeUri) { Content = content };

            Task<HttpResponseMessage> responseTask = client.SendAsync(request);
            HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);

            await router.Route(context, NullServiceProvider.Instance).ConfigureAwait(false);

            using HttpResponseMessage response = await responseTask.ConfigureAwait(false);

            return new ResponseSnapshot
            (
                response.StatusCode,
                response.Content.Headers.ContentType?.MediaType,
                await response.Content.ReadAsStringAsync().ConfigureAwait(false)
            );
        }

        private static int GetFreePort()
        {
            using TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();

            int port = ((IPEndPoint) listener.LocalEndpoint).Port;

            listener.Stop();
            return port;
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"{message} Expected: {expected}. Actual: {actual}.");
        }

        private readonly record struct ResponseSnapshot(HttpStatusCode StatusCode, string? MediaType, string Body);

        private sealed class NullServiceProvider : IServiceProvider
        {
            public static NullServiceProvider Instance { get; } = new();

            public object? GetService(Type serviceType) => null;
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
