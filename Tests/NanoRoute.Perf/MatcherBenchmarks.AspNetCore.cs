/********************************************************************************
* MatcherBenchmarks.AspNetCore.cs                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace NanoRoute.Perf
{
    public partial class MatcherBenchmarks
    {
        private sealed class AspNetCoreRouteMatcherFactory : IRouteMatcherFactory
        {
            private sealed class AspNetCoreRouteMatcher(RoutingBenchmarkScenario scenario) : IRouteMatcher
            {
                private readonly AspNetCoreRouteEndpointInvoker _invoker = new
                (
                    new RouteEndpoint
                    (
                        requestDelegate: static _ => Task.CompletedTask,
                        routePattern: RoutePatternFactory.Parse(scenario.Pattern),
                        order: 0,
                        metadata: new EndpointMetadataCollection(new HttpMethodMetadata([HttpMethods.Get])),
                        displayName: scenario.Pattern
                    ),
                    scenario.RequestUri
                );

                public ValueTask Match() => new(_invoker.MatchEndpoint());

                public void Dispose() => _invoker.Dispose();
            }

            public IRouteMatcher Create(RoutingBenchmarkScenario scenario) => new AspNetCoreRouteMatcher(scenario);

            public override string ToString() => "ASP.NET Core Matcher";
        }
    }
}
