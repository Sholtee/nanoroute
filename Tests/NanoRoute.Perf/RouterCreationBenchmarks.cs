/********************************************************************************
* RouterCreationBenchmarks.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    [MemoryDiagnoser]
    public class RouterCreationBenchmarks
    {
        public enum RouteShape
        {
            Literal,
            Mixed
        }

        private static readonly RequestMiddlewareDelegate s_handler =
            static (_, _) => throw new InvalidOperationException("The router creation benchmark should not execute handlers.");

        private RouterBuilder<TestRouter, RouterConfig> _builder = null!;

        [Params(10, 100, 1000)]
        public int RouteCount { get; set; }

        [Params(RouteShape.Literal, RouteShape.Mixed)]
        public RouteShape Shape { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _builder = new RouterBuilder<TestRouter, RouterConfig>(static builder => new TestRouter(builder))
                .AddDefaultValueParsers();

            for (int i = 0; i < RouteCount; i++)
                _builder.AddHandler("GET", CreateRoutePattern(i), s_handler);
        }

        [Benchmark]
        public Router CreateRouter() => _builder.CreateRouter();

        private string CreateRoutePattern(int index) => Shape switch
        {
            RouteShape.Literal => $"/api/v1/tenants/{index}/users/{index}/orders/{index}/details/",
            RouteShape.Mixed when index % 2 is 0 => $"/api/v1/tenants/{index}/users/{{userId:int}}/orders/{index}/details/",
            RouteShape.Mixed => $"/api/v1/tenants/{index}/users/{index}/orders/{{orderId:int}}/details/",
            _ => throw new InvalidOperationException($"Unknown route shape: {Shape}.")
        };

        private sealed class TestRouter(RouterBuilder<TestRouter, RouterConfig> builder) : Router(builder, builder.RouterConfig)
        {
        }
    }
}
