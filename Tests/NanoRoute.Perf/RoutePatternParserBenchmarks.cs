/********************************************************************************
* RoutePatternParserBenchmarks.cs                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    using Internals;

    [MemoryDiagnoser]
    public class RoutePatternParserRouteBenchmarks
    {
        public enum ScenarioKind
        {
            Empty,
            LiteralSegments,
            SingleParameter,
            MixedParameters,
            ParserArguments,
            LongRoute
        }

        private string _pattern = string.Empty;

        [Params(ScenarioKind.Empty, ScenarioKind.LiteralSegments, ScenarioKind.SingleParameter, ScenarioKind.MixedParameters, ScenarioKind.ParserArguments, ScenarioKind.LongRoute)]
        public ScenarioKind Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _pattern = Scenario switch
            {
                ScenarioKind.Empty => "/",
                ScenarioKind.LiteralSegments => "/api/v1/catalog/items/details/",
                ScenarioKind.SingleParameter => "/items/{id:int}/",
                ScenarioKind.MixedParameters => "/tenants/{tenantId:guid}/items/{itemId:int}/revisions/{revision:int}/",
                ScenarioKind.ParserArguments => "/items/{id:int(min=1,max=999999)}/names/{name:str(pattern='[a-z]+')}/details/",
                ScenarioKind.LongRoute => "/api/v1/tenants/{tenantId:guid}/regions/{region:str}/warehouses/{warehouseId:int}/aisles/{aisle:str}/bins/{binId:int}/items/{itemId:guid}/",
                _ => throw new ArgumentOutOfRangeException(nameof(Scenario), Scenario, "Unknown route-pattern benchmark scenario.")
            };
        }

        [Benchmark]
        public int ParseRoutePattern()
        {
            int count = 0;

            foreach (object definition in DslParser.ParseRoutePattern(_pattern))
            {
                _ = definition;
                count++;
            }

            return count;
        }
    }

    [MemoryDiagnoser]
    public class RoutePatternParserQueryBenchmarks
    {
        public enum ScenarioKind
        {
            Empty,
            SingleParameter,
            OptionalParameter,
            ListParameter,
            ParserArguments,
            ManyParameters
        }

        private string _pattern = string.Empty;

        [Params(ScenarioKind.Empty, ScenarioKind.SingleParameter, ScenarioKind.OptionalParameter, ScenarioKind.ListParameter, ScenarioKind.ParserArguments, ScenarioKind.ManyParameters)]
        public ScenarioKind Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _pattern = Scenario switch
            {
                ScenarioKind.Empty => string.Empty,
                ScenarioKind.SingleParameter => "{filter:str}",
                ScenarioKind.OptionalParameter => "{page?:int(min=1)}",
                ScenarioKind.ListParameter => "{ids:guid[]}",
                ScenarioKind.ParserArguments => "{filter:str(pattern='a&b')}&{page?:int(min=1)}&{limit:int(min=1,max=100)}",
                ScenarioKind.ManyParameters => "{tenantId:guid}&{region:str}&{warehouseId:int}&{aisle:str}&{binId:int}&{itemId:guid}&{includeInactive?:bool}",
                _ => throw new ArgumentOutOfRangeException(nameof(Scenario), Scenario, "Unknown query-pattern benchmark scenario.")
            };
        }

        [Benchmark]
        public int ParseQueryPattern()
        {
            int count = 0;

            foreach (ParameterDefinition definition in DslParser.ParseQueryPattern(_pattern))
            {
                _ = definition;
                count++;
            }

            return count;
        }
    }
}
