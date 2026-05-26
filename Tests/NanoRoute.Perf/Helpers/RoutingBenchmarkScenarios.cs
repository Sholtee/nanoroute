/********************************************************************************
* RoutingBenchmarkScenarios.cs                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    public abstract class RoutingBenchmarkScenarios
    {
        public enum RouteScenarioKind
        {
            SingleLiteral,
            SingleParsed,
            ComplexLiteral,
            ComplexParsed
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "BenchmarkDotNet requires parameter sources to be public")]
        public readonly record struct RoutingBenchmarkScenario(RouteScenarioKind Kind, string Pattern, Uri RequestUri);

        protected RoutingBenchmarkScenario Scenario { get; private set; }

        [Params(RouteScenarioKind.SingleLiteral, RouteScenarioKind.SingleParsed, RouteScenarioKind.ComplexLiteral, RouteScenarioKind.ComplexParsed)]
        public RouteScenarioKind ScenarioKind
        {
            get;
            set
            {
                Scenario = (field = value) switch
                {
                    RouteScenarioKind.SingleLiteral => new RoutingBenchmarkScenario(value, "/health/", new Uri("https://www.example.com/health")),
                    RouteScenarioKind.SingleParsed => new RoutingBenchmarkScenario(value, "/{id:int}/", new Uri("https://www.example.com/42")),
                    RouteScenarioKind.ComplexLiteral => new RoutingBenchmarkScenario(value, "/api/v1/users/42/orders/7/items/3/details/", new Uri("https://www.example.com/api/v1/users/42/orders/7/items/3/details")),
                    RouteScenarioKind.ComplexParsed => new RoutingBenchmarkScenario(value, "/api/v1/users/{userId:int}/orders/{orderId:int}/items/{itemId:int}/details/", new Uri("https://www.example.com/api/v1/users/42/orders/7/items/3/details")),
                    _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown routing benchmark scenario.")
                };
            }
        }
    }
}
