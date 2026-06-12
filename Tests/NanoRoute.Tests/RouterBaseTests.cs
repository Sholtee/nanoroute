/********************************************************************************
* RouterBaseTests.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    [TestFixture]
    internal sealed class RouterBaseTests
    {
        private static readonly IServiceProvider s_services = new ServiceProvider();

        private sealed class ServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }

        private sealed class TestRouter(RouteScopeBuilder routes, RouterConfig config) : RouterBase<RouterConfig>(routes, config)
        {
            public Task<HttpResponseMessage> RouteAsync(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation = default) => Route(request, services, cancellation);
        }

        [Test]
        public void Constructor_ShouldStoreConfig()
        {
            RouterConfig config = new()
            {
                MatchingPrecedence = MatchingPrecedence.ParameterizedFirst
            };

            TestRouter router = new(new RouteScopeBuilder(), config);

            Assert.That(router.Config, Is.SameAs(config));
        }

        [Test]
        public void Constructor_ShouldBeNullChecked()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => new TestRouter(null!, new RouterConfig()))!;
            Assert.That(ex.ParamName, Is.EqualTo("routes"));

            ex = Assert.Throws<ArgumentNullException>(() => new TestRouter(new RouteScopeBuilder(), null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("config"));
        }

        [Test]
        public async Task Route_ShouldExecuteCapturedRouteSnapshot()
        {
            RouteScopeBuilder routes = new RouteScopeBuilder()
                .AddHandler("GET", "/health/", static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("ok")
                }));
            TestRouter router = new(routes, new RouterConfig());

            routes.AddHandler("GET", "/later/", static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));

            using HttpRequestMessage request = new(HttpMethod.Get, "https://example.test/health");
            using HttpResponseMessage response = await router.RouteAsync(request, s_services);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("ok"));
        }

        [Test]
        public void Route_ShouldBeNullChecked()
        {
            TestRouter router = new(new RouteScopeBuilder(), new RouterConfig());
            using HttpRequestMessage request = new(HttpMethod.Get, "https://example.test/health");

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => router.RouteAsync(null!, s_services))!;
            Assert.That(ex.ParamName, Is.EqualTo("request"));

            ex = Assert.ThrowsAsync<ArgumentNullException>(async () => await router.RouteAsync(request, null!).ConfigureAwait(false))!;
            Assert.That(ex.ParamName, Is.EqualTo("services"));
        }
    }
}
