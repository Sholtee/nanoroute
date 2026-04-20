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
        public enum ScenarioKind
        {
            SingleLiteral,
            SingleParsed,
            ComplexLiteral,
            ComplexParsed
        }

        private static readonly HttpResponseMessage s_response = new(HttpStatusCode.OK);

        private static readonly IServiceProvider s_services = new NoopServiceProvider();

        private TestRouter _router = null!;

        private HttpRequestMessage _request = null!;

        [Params(ScenarioKind.SingleLiteral, ScenarioKind.SingleParsed, ScenarioKind.ComplexLiteral, ScenarioKind.ComplexParsed)]
        public ScenarioKind Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            RouterBuilder<TestRouter, RouterConfig> builder = new(static bldr => new TestRouter(bldr));
            builder.WithConfiguration(static cfg => cfg.Timeout = Timeout.InfiniteTimeSpan);

            switch (Scenario)
            {
                case ScenarioKind.SingleLiteral:
                    builder.AddHandler("GET", "/health", static (_, _) => Task.FromResult(s_response));
                    _request = CreateRequest("/health");
                    break;

                case ScenarioKind.SingleParsed:
                    builder
                        .AddIntParser()
                        .AddHandler("GET", "/{id:int}", static (_, _) => Task.FromResult(s_response));
                    _request = CreateRequest("/42");
                    break;

                case ScenarioKind.ComplexLiteral:
                    builder.AddHandler("GET", "/api/v1/users/42/orders/7/items/3/details", static (_, _) => Task.FromResult(s_response));
                    _request = CreateRequest("/api/v1/users/42/orders/7/items/3/details");
                    break;

                case ScenarioKind.ComplexParsed:
                    builder
                        .AddDefaultValueParsers()
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

