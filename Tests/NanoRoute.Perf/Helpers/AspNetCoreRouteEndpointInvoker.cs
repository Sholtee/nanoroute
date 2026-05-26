/********************************************************************************
* AspNetCoreRouteEndpointInvoker.cs                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace NanoRoute.Perf
{
    internal sealed class AspNetCoreRouteEndpointInvoker : IDisposable
    {
        private static readonly Type s_matcherFactoryType = Type.GetType("Microsoft.AspNetCore.Routing.Matching.MatcherFactory, Microsoft.AspNetCore.Routing", throwOnError: true)!;

        private readonly ServiceProvider _services;

        private readonly Func<HttpContext, Task> _match;

        private readonly DefaultHttpContext _context;

        public AspNetCoreRouteEndpointInvoker(RouteEndpoint endpoint, Uri requestUri, ServiceProvider? services = null)
        {
            _services = services ?? CreateServices();

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

        public static ServiceProvider CreateServices() => new ServiceCollection()
            .AddLogging(static _ => { })
            .AddRouting()
            .BuildServiceProvider();

        public async Task MatchEndpoint()
        {
            // Reset the context
            _context.Request.RouteValues.Clear();
            _context.SetEndpoint(null);

            await _match(_context).ConfigureAwait(false);

            EnsureMatchedEndpoint();
        }

        public async Task DispatchEndpoint()
        {
            await MatchEndpoint().ConfigureAwait(false);

            await EnsureMatchedEndpoint().RequestDelegate!(_context).ConfigureAwait(false);
        }

        public void Dispose() => _services.Dispose();

        private RouteEndpoint EnsureMatchedEndpoint() => _context.GetEndpoint() as RouteEndpoint ?? throw new InvalidOperationException($"Failed to match '{_context.Request}'");
    }
}
