/********************************************************************************
* EndpointDispatchBenchmarks.NanoRoute.cs                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.Perf
{
    public partial class EndpointDispatchBenchmarks
    {
        private sealed class NanoRouteEndpointDispatcherFactory : IEndpointDispatcherFactory
        {
            private sealed class NanoRouteEndpointDispatcher(RoutingBenchmarkScenario scenario) : IEndpointDispatcher
            {
                private static readonly IServiceProvider s_services = new NoopServiceProvider();

                private static readonly Task<HttpResponseMessage> s_responseTask = Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

                private readonly TestRouter _router = TestRouter
                    .CreateBuilder()
                    .AddDefaultValueParsers()
                    .AddHandler("GET", scenario.Pattern, static (_, _) => s_responseTask)
                    .CreateRouter();

                private readonly HttpRequestMessage _request = new(HttpMethod.Get, scenario.RequestUri);

                public void Dispose() => _request.Dispose();

                public Task Dispatch() => _router.Route(_request, s_services);

                private sealed class TestRouter(RouterBuilder<TestRouter, RouterConfig> builder)
                {
                    private readonly RequestPipeline _pipeline = new(builder, builder.RouterConfig.MatchingPrecedence);

                    public static RouterBuilder<TestRouter, RouterConfig> CreateBuilder() => new(static builder => new TestRouter(builder));

                    public Task<HttpResponseMessage> Route(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation = default) =>
                        _pipeline.ExecuteAsync(request, services, cancellation);
                }

                private sealed class NoopServiceProvider : IServiceProvider
                {
                    public object? GetService(Type serviceType) => null;
                }
            }

            public IEndpointDispatcher Create(RoutingBenchmarkScenario scenario) => new NanoRouteEndpointDispatcher(scenario);

            public override string ToString() => "NanoRoute Router";
        }
    }
}
