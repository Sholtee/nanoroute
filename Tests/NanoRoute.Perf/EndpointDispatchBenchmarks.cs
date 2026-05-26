/********************************************************************************
* EndpointDispatchBenchmarks.cs                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    [MemoryDiagnoser]
    public partial class EndpointDispatchBenchmarks: RoutingBenchmarkScenarios
    {
        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "BenchmarkDotNet requires EndpointDispatcherFactories to be public")]
        public interface IEndpointDispatcher : IDisposable
        {
            Task Dispatch();
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "BenchmarkDotNet requires EndpointDispatcherFactories to be public")]
        public interface IEndpointDispatcherFactory
        {
            IEndpointDispatcher Create(RoutingBenchmarkScenario scenario);
        }

        public static IEnumerable<IEndpointDispatcherFactory> EndpointDispatcherFactories
        {
            get
            {
                yield return new AspNetCoreEndpointDispatcherFactory();
                yield return new NanoRouteEndpointDispatcherFactory();
            }
        }

        [ParamsSource(nameof(EndpointDispatcherFactories))]
        public IEndpointDispatcherFactory EndpointDispatcherFactory { get; set; } = null!;

        private IEndpointDispatcher _dispatcher = null!;

        [GlobalSetup]
        public void Setup() => _dispatcher = EndpointDispatcherFactory.Create(Scenario);

        [GlobalCleanup]
        public void Cleanup() => _dispatcher.Dispose();

        [Benchmark]
        public Task Dispatch() => _dispatcher.Dispatch();
    }
}
