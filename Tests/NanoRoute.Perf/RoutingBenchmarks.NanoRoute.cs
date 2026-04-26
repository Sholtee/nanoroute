/********************************************************************************
* RoutingBenchmarks.NanoRoute.cs                                                *
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
    public partial class RoutingBenchmarks
    {
        private sealed class NanoRouteRouterFactory : IRouterFactory
        {
            private sealed class NanoRouteRouter(string routePattern, Uri requestUri) : IRouter
            {
                private static readonly HttpResponseMessage s_response = new(HttpStatusCode.OK);

                private static readonly IServiceProvider s_services = new NoopServiceProvider();

                private readonly TestRouter _router = new RouterBuilder<TestRouter, RouterConfig>(static bldr => new TestRouter(bldr))
                    .WithConfiguration(static cfg => cfg.Timeout = Timeout.InfiniteTimeSpan)
                    .AddDefaultValueParsers()
                    .AddHandler("GET", routePattern, static (_, _) => Task.FromResult(s_response))
                    .CreateRouter();

                private readonly HttpRequestMessage _request = new(HttpMethod.Get, requestUri);

                public void Dispose() => _request.Dispose();

                public Task Match() => _router.Route(_request, s_services);

                private sealed class TestRouter(RouterBuilder<TestRouter, RouterConfig> builder) : Router(builder, builder.RouterConfig)
                {
                    public Task<HttpResponseMessage> Route(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation = default) => Handle(request, services, cancellation);
                }

                private sealed class NoopServiceProvider : IServiceProvider
                {
                    public object? GetService(Type serviceType) => null;
                }
            }

            public IRouter Create(string pattern, Uri requestUri) => new NanoRouteRouter(pattern, requestUri);

            public override string ToString() => "NanoRoute Router";
        }
    }
}
