/********************************************************************************
* RoutingBenchmarks.AspNetCore.cs                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace NanoRoute.Perf
{
    public partial class RoutingBenchmarks
    {
        private sealed class AspNetCoreRouterFactory : IRouterFactory
        {
            private sealed class AspNetCoreRouter : IRouter
            {
                private static readonly Type s_matcherFactoryType = Type.GetType("Microsoft.AspNetCore.Routing.Matching.MatcherFactory, Microsoft.AspNetCore.Routing", throwOnError: true)!;

                private readonly ServiceProvider _services = new ServiceCollection()
                    .AddLogging(static _ => { })
                    .AddRouting()
                    .BuildServiceProvider();

                private readonly Func<HttpContext, Task> _match;

                private readonly DefaultHttpContext _context;

                public AspNetCoreRouter(string routePattern, Uri requestUri)
                {
                    RouteEndpoint endpoint = new
                    (
                        requestDelegate: static context =>
                        {
                            // ASP doesn't parse route values during matching (in contrast of NanoRoute)
                            foreach(string val in context.Request.RouteValues.Values!)
                                _ = int.Parse(val, NumberStyles.Integer, CultureInfo.InvariantCulture);

                            context.Response.StatusCode = StatusCodes.Status200OK;
                            return Task.CompletedTask;
                        },
                        routePattern: RoutePatternFactory.Parse(routePattern),
                        order: 0,
                        metadata: new EndpointMetadataCollection(new HttpMethodMetadata(["GET"])),
                        displayName: routePattern
                    );

                    object
                        matcherFactory = _services.GetRequiredService(s_matcherFactoryType),
                        matcher = matcherFactory
                            .GetType()
                            .GetMethod("CreateMatcher", BindingFlags.Instance | BindingFlags.Public, [typeof(EndpointDataSource)])!
                            .Invoke(matcherFactory, [new DefaultEndpointDataSource(endpoint)])!;

                    _match = matcher
                        .GetType()
                        .GetMethod("MatchAsync", BindingFlags.Instance | BindingFlags.Public, [typeof(HttpContext)])!
                        .CreateDelegate<Func<HttpContext, Task>>(matcher);

                    _context = new DefaultHttpContext
                    {
                        RequestServices = _services
                    };
                    _context.Request.Method = HttpMethods.Get;
                    _context.Request.Scheme = requestUri.Scheme;
                    _context.Request.Host = new HostString(requestUri.Host);
                    _context.Request.Path = new PathString(requestUri.AbsolutePath);
                }

                public async Task Match()
                {
                    _context.Request.RouteValues.Clear();
                    _context.SetEndpoint(null);

                    await _match(_context).ConfigureAwait(false);

                    if (_context.GetEndpoint() is not { } endpoint)
                        throw new InvalidOperationException($"Failed to match '{_context.Request}'");

                    await endpoint.RequestDelegate!(_context).ConfigureAwait(false);
                }

                public void Dispose() => _services.Dispose();
            }

            public IRouter Create(string pattern, Uri requestUri) => new AspNetCoreRouter(pattern, requestUri);

            public override string ToString() => "ASP.NET Core Matcher";
        }
    }
}

