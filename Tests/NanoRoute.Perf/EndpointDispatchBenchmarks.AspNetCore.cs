/********************************************************************************
* EndpointDispatchBenchmarks.AspNetCore.cs                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace NanoRoute.Perf
{
    public partial class EndpointDispatchBenchmarks
    {
        private sealed class AspNetCoreEndpointDispatcherFactory : IEndpointDispatcherFactory
        {
            private sealed class AspNetCoreEndpointDispatcher : IEndpointDispatcher
            {
                private readonly AspNetCoreRouteEndpointInvoker _invoker;

                public AspNetCoreEndpointDispatcher(RoutingBenchmarkScenario scenario)
                {
                    ServiceProvider services = AspNetCoreRouteEndpointInvoker.CreateServices();
                    RoutePattern routePatternDefinition = RoutePatternFactory.Parse(scenario.Pattern);
                    RequestDelegateResult requestDelegateResult = RequestDelegateFactory.Create
                    (
                        scenario.Kind switch
                        {
                            RouteScenarioKind.SingleLiteral or RouteScenarioKind.ComplexLiteral => LiteralHandler,
                            RouteScenarioKind.SingleParsed => SingleParsedHandler,
                            RouteScenarioKind.ComplexParsed => ComplexParsedHandler,
                            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown endpoint-dispatch benchmark scenario.")
                        },
                        new RequestDelegateFactoryOptions
                        {
                            DisableInferBodyFromParameters = true,
                            RouteParameterNames = routePatternDefinition.Parameters.Select(static parameter => parameter.Name).ToArray(),
                            ServiceProvider = services,
                            ThrowOnBadRequest = true
                        }
                    );

                    object[] metadata = new object[requestDelegateResult.EndpointMetadata.Count + 1];
                    metadata[0] = new HttpMethodMetadata([HttpMethods.Get]);

                    for (int i = 0; i < requestDelegateResult.EndpointMetadata.Count; i++)
                        metadata[i + 1] = requestDelegateResult.EndpointMetadata[i];

                    _invoker = new AspNetCoreRouteEndpointInvoker
                    (
                        new RouteEndpoint
                        (
                            requestDelegate: requestDelegateResult.RequestDelegate,
                            routePattern: routePatternDefinition,
                            order: 0,
                            metadata: new EndpointMetadataCollection(metadata),
                            displayName: scenario.Pattern
                        ),
                        scenario.RequestUri,
                        services
                    );

                    static Task LiteralHandler() => Task.CompletedTask;

                    static Task SingleParsedHandler(int id) => Task.CompletedTask;

                    static Task ComplexParsedHandler(int userId, int orderId, int itemId) => Task.CompletedTask;
                }

                public Task Dispatch() => _invoker.DispatchEndpoint();

                public void Dispose() => _invoker.Dispose();
            }

            public IEndpointDispatcher Create(RoutingBenchmarkScenario scenario) => new AspNetCoreEndpointDispatcher(scenario);

            public override string ToString() => "ASP.NET Core Minimal API";
        }
    }
}
