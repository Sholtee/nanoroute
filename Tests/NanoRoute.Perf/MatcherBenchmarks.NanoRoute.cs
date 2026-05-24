/********************************************************************************
* MatcherBenchmarks.NanoRoute.cs                                                *
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
    using Internals;

    public partial class MatcherBenchmarks
    {
        private sealed class NanoRouteMatcherFactory : IRouteMatcherFactory
        {
            private sealed class NanoRouteMatcher : IRouteMatcher
            {
                private static readonly IServiceProvider s_services = new NoopServiceProvider();

                private static readonly Task<HttpResponseMessage> s_responseTask = Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

                private readonly RouteNode _root;

                private readonly RouterConfig _config;

                private readonly Uri _requestUri;

                public NanoRouteMatcher(RoutingBenchmarkScenario scenario)
                {
                    RouterBuilder<TestRouter, RouterConfig> builder = TestRouter
                        .CreateBuilder()
                        .AddDefaultValueParsers()
                        .AddHandler("GET", scenario.Pattern, static (_, _) => s_responseTask);

                    _root = builder.CreateSnapshot();
                    _config = builder.RouterConfig;
                    _requestUri = scenario.RequestUri;
                }

                public async ValueTask Match()
                {
                    RouteMatchCursor cursor = new(_root, HttpVerb.Get, _requestUri, s_services, _config, CancellationToken.None);

                    await using (cursor.ConfigureAwait(false))
                    {
                        if (!await cursor.MoveNextAsync().ConfigureAwait(false))
                            throw new InvalidOperationException($"Failed to match '{_requestUri}'.");
                    }
                }

                public void Dispose() { }

                private sealed class TestRouter(RouterBuilder<TestRouter, RouterConfig> builder) : Router<TestRouter, RouterConfig>(builder);

                private sealed class NoopServiceProvider : IServiceProvider
                {
                    public object? GetService(Type serviceType) => null;
                }
            }

            public IRouteMatcher Create(RoutingBenchmarkScenario scenario) => new NanoRouteMatcher(scenario);

            public override string ToString() => "NanoRoute Match Cursor";
        }
    }
}
