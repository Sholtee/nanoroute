/********************************************************************************
* EndpointDispatchBenchmarks.NanoRoute.cs                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Net.Http;
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

                private readonly InMemoryRouter _router = InMemoryRouter
                    .CreateBuilder()
                    .AddDefaultValueParsers()
                    .AddHandler("GET", scenario.Pattern, static (_, _) => s_responseTask)
                    .CreateRouter();

                private readonly HttpRequestMessage _request = new(HttpMethod.Get, scenario.RequestUri);

                public void Dispose() => _request.Dispose();

                public Task Dispatch() => _router.Route(_request, s_services);

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
