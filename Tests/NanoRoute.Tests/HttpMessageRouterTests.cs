/********************************************************************************
* HttpMessageRouterTests.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    [TestFixture]
    internal sealed class HttpMessageRouterTests
    {
        private static readonly IServiceProvider s_services = new ServiceProvider();

        private sealed class ServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }

        [Test]
        public void CreateBuilder_ShouldCreateRouterWithConfigSnapshot()
        {
            RouterBuilder<HttpMessageRouter, RouterConfig> builder = HttpMessageRouter.CreateBuilder();

            HttpMessageRouter router = builder.CreateRouter(static config =>
            {
                config.MatchingPrecedence = MatchingPrecedence.ParameterizedFirst;
            });

            HttpMessageRouter defaultRouter = builder.CreateRouter();

            Assert.That(router.Config.MatchingPrecedence, Is.EqualTo(MatchingPrecedence.ParameterizedFirst));
            Assert.That(defaultRouter.Config.MatchingPrecedence, Is.EqualTo(MatchingPrecedence.LiteralFirst));
        }

        [Test]
        public async Task Route_ShouldProcessHttpRequestMessage()
        {
            HttpMessageRouter router = HttpMessageRouter
                .CreateBuilder()
                .AddHandler("GET", "/health/", static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("ok")
                }))
                .CreateRouter();

            using HttpRequestMessage request = new(HttpMethod.Get, "https://example.test/health");
            using HttpResponseMessage response = await router.Route(request, s_services);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("ok"));
        }

        [Test]
        public void Route_ShouldBeNullChecked()
        {
            HttpMessageRouter router = HttpMessageRouter.CreateBuilder().CreateRouter();
            HttpRequestMessage request = new(HttpMethod.Get, "https://example.test/health");

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => router.Route(null!, s_services))!;
            Assert.That(ex.ParamName, Is.EqualTo("request"));

            ex = Assert.Throws<ArgumentNullException>(() => router.Route(request, null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("services"));
        }
    }
}
