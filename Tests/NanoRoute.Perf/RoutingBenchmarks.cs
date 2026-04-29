/********************************************************************************
* RoutingBenchmarks.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    public interface IRouter : IDisposable
    {
        Task Match();
    }

    public interface IRouterFactory
    {
        IRouter Create(string pattern, Uri requestUri);
    }

    [MemoryDiagnoser]
    public partial class RoutingBenchmarks
    {
        public enum ScenarioKind
        {
            SingleLiteral,
            SingleParsed,
            ComplexLiteral,
            ComplexParsed
        }

        [Params(ScenarioKind.SingleLiteral, ScenarioKind.SingleParsed, ScenarioKind.ComplexLiteral, ScenarioKind.ComplexParsed)]
        public ScenarioKind Scenario { get; set; }

        public static IEnumerable<IRouterFactory> RouterFactories
        {
            get
            {
                yield return new AspNetCoreRouterFactory();
                yield return new NanoRouteRouterFactory();
            }
        }

        [ParamsSource(nameof(RouterFactories))]
        public IRouterFactory RouterFactory { get; set; } = null!;

        private IRouter _router = null!;

        [GlobalSetup]
        public void Setup()
        {
            (string pattern, Uri requestUri) = Scenario switch
            {
                ScenarioKind.SingleLiteral => ("/health", new Uri("https://www.example.com/health")),
                ScenarioKind.SingleParsed => ("/{id:int}", new Uri("https://www.example.com/42")),
                ScenarioKind.ComplexLiteral => ("/api/v1/users/42/orders/7/items/3/details", new Uri("https://www.example.com/api/v1/users/42/orders/7/items/3/details")),
                ScenarioKind.ComplexParsed => ("/api/v1/users/{userId:int}/orders/{orderId:int}/items/{itemId:int}/details", new Uri("https://www.example.com/api/v1/users/42/orders/7/items/3/details")),
                _ => throw new ArgumentOutOfRangeException(nameof(Scenario), Scenario, "Unknown routing benchmark scenario.")
            };

            _router = RouterFactory.Create(pattern, requestUri);
        }

        [GlobalCleanup]
        public void Cleanup() => _router.Dispose();

        [Benchmark]
        public Task Route() => _router.Match();
    }
}

