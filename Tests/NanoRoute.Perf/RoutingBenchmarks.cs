/********************************************************************************
* RoutingBenchmarks.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    [MemoryDiagnoser]
    public class RoutingBenchmarks
    {
        private static readonly HttpResponseMessage s_response = new(HttpStatusCode.OK);

        private static readonly IServiceProvider s_services = new NoopServiceProvider();

        private TestRouter _router = null!;

        private HttpRequestMessage _request = null!;

        [Params("single-literal", "single-parsed", "complex-literal", "complex-parsed")]
        public string Scenario { get; set; } = string.Empty;

        [GlobalSetup]
        public void Setup()
        {
            RouterBuilder<TestRouter, RouterConfig> builder = new(static bldr => new TestRouter(bldr));
            builder.WithConfiguration(static cfg => cfg.Timeout = Timeout.InfiniteTimeSpan);

            switch (Scenario)
            {
                case "single-literal":
                    builder.AddHandler("GET", "/health", static (_, _) => Task.FromResult(s_response));
                    _request = CreateRequest("/health");
                    break;

                case "single-parsed":
                    builder
                        .AddIntParser()
                        .AddHandler("GET", "/{id:int}", static (_, _) => Task.FromResult(s_response));
                    _request = CreateRequest("/42");
                    break;

                case "complex-literal":
                    builder.AddHandler("GET", "/api/v1/users/42/orders/7/items/3/details", static (_, _) => Task.FromResult(s_response));
                    _request = CreateRequest("/api/v1/users/42/orders/7/items/3/details");
                    break;

                case "complex-parsed":
                    builder
                        .AddDefaultParsers()
                        .AddHandler("GET", "/api/v1/users/{userId:int}/orders/{orderId:int}/items/{itemId:int}/details", static (_, _) => Task.FromResult(s_response));
                    _request = CreateRequest("/api/v1/users/42/orders/7/items/3/details");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(Scenario), Scenario, "Unknown routing benchmark scenario.");
            }

            _router = builder.CreateRouter();
        }

        [Benchmark]
        public Task<HttpResponseMessage> Route() => _router.Route(_request, s_services);

        private static HttpRequestMessage CreateRequest(string path) => new(HttpMethod.Get, $"https://www.example.com{path}");

        private sealed class TestRouter(RouterBuilder<TestRouter, RouterConfig> builder) : Router(builder, builder.RouterConfig)
        {
            public Task<HttpResponseMessage> Route(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation = default) => Handle(request, services, cancellation);
        }

        private sealed class NoopServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }
    }
}
