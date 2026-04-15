/********************************************************************************
* Program.Infrastructure.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NanoRoute.NativeAot
{
    internal static partial class Program
    {
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
                response.Headers.TryGetValues("X-Typed-Id", out IEnumerable<string>? typedIds) ? string.Join(",", typedIds) : null,
                await response.Content.ReadAsStringAsync().ConfigureAwait(false)
            );
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"{message} Expected: {expected}. Actual: {actual}.");
        }

        private static int GetFreePort()
        {
            using TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();

            int port = ((IPEndPoint) listener.LocalEndpoint).Port;

            listener.Stop();
            return port;
        }

        private readonly record struct ResponseSnapshot(HttpStatusCode StatusCode, string? MediaType, string? TypedIdHeader, string Body);

        private sealed class NullServiceProvider : IServiceProvider
        {
            public static NullServiceProvider Instance { get; } = new();

            public object? GetService(Type serviceType) => serviceType == typeof(GreetingService) ? new GreetingService("typed") : null;
        }
    }
}
