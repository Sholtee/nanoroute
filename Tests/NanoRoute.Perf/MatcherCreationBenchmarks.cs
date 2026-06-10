/********************************************************************************
* MatcherCreationBenchmarks.cs                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    using Internals;

    [MemoryDiagnoser]
    public class MatcherCreationBenchmarks
    {
        public enum RouteShape
        {
            Literal,
            Mixed
        }

        private static readonly RequestHandlerDelegate s_handler =
            static (_, _) => throw new InvalidOperationException("The router creation benchmark should not execute handlers.");

        private static readonly Uri s_uri = new("https://google.co.hu", UriKind.Absolute);

        private RouteNode? _root;

        [Params(10, 100, 1000)]
        public int RouteCount { get; set; }

        [Params(RouteShape.Literal, RouteShape.Mixed)]
        public RouteShape Shape { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            RouterBuilder<InMemoryRouter, RouterConfig> builder = InMemoryRouter
                .CreateBuilder()
                .AddDefaultValueParsers();

            for (int i = 0; i < RouteCount; i++)
                builder.AddHandler("GET", CreateRoutePattern(i), s_handler);

            _root = builder.CreateSnapshot();
        }

        [Benchmark]
        public object CreateRouter() => new RouteMatchCursor(_root!, HttpVerb.Get, s_uri, null!, null!, MatchingPrecedence.LiteralFirst, default);

        private string CreateRoutePattern(int index) => Shape switch
        {
            RouteShape.Literal => $"/api/v1/tenants/{index}/users/{index}/orders/{index}/details/",
            RouteShape.Mixed when index % 2 is 0 => $"/api/v1/tenants/{index}/users/{{userId:int}}/orders/{index}/details/",
            RouteShape.Mixed => $"/api/v1/tenants/{index}/users/{index}/orders/{{orderId:int}}/details/",
            _ => throw new InvalidOperationException($"Unknown route shape: {Shape}.")
        };
    }
}
