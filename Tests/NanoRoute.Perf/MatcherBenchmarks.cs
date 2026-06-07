/********************************************************************************
* MatcherBenchmarks.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;

namespace NanoRoute.Perf
{
    [Config(typeof(MatcherBenchmarksConfig))]
    public partial class MatcherBenchmarks : RoutingBenchmarkScenarios
    {
        private sealed class MatcherBenchmarksConfig : ManualConfig
        {
            public MatcherBenchmarksConfig()
            {
                AddDiagnoser(MemoryDiagnoser.Default);
                AddExporter(RPlotExporter.Default);
            }
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "BenchmarkDotNet requires RouteMatcherFactories to be public")]
        public interface IRouteMatcher : IDisposable
        {
            ValueTask Match();
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "BenchmarkDotNet requires RouteMatcherFactories to be public")]
        public interface IRouteMatcherFactory
        {
            IRouteMatcher Create(RoutingBenchmarkScenario scenario);
        }

        public static IEnumerable<IRouteMatcherFactory> RouteMatcherFactories
        {
            get
            {
                yield return new AspNetCoreRouteMatcherFactory();
                yield return new NanoRouteMatcherFactory();
            }
        }

        [ParamsSource(nameof(RouteMatcherFactories))]
        public IRouteMatcherFactory RouteMatcherFactory { get; set; } = null!;

        private IRouteMatcher _matcher = null!;

        [GlobalSetup]
        public void Setup() => _matcher = RouteMatcherFactory.Create(Scenario);

        [GlobalCleanup]
        public void Cleanup() => _matcher.Dispose();

        [Benchmark]
        public ValueTask Match() => _matcher.Match();
    }
}
