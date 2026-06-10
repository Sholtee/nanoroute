/********************************************************************************
* RouterBuilderTests.Prefix.cs                                                  *
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
    using Properties;

    internal sealed partial class RouterBuilderTests
    {
        [Test]
        public async Task CreatePrefix_ShouldRegisterHandlersUnderThePrefix()
        {
            _routerBuilder
                .AddHandler("GET", "/users/", async (_, _) => new HttpResponseMessage(HttpStatusCode.Accepted));

            _routerBuilder
                .CreatePrefix("/api/*")
                .AddHandler("GET", "/users/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("prefixed")
                });

            HttpMessageRouter router = _routerBuilder.CreateRouter();

            HttpResponseMessage prefixedResponse = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/api/users"),
                s_services
            );

            HttpResponseMessage rootResponse = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/users"),
                s_services
            );

            Assert.Multiple(() =>
            {
                Assert.That(prefixedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(rootResponse.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
            });
            Assert.That(await prefixedResponse.Content.ReadAsStringAsync(), Is.EqualTo("prefixed"));
        }

        [Test]
        public void CreatePrefix_ShouldKeepChildValueParsersScopedToTheChildBranch()
        {
            RouteScopeBuilder api = _routerBuilder.CreatePrefix("/api/*");

            api
                .AddValueParser("name", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = segment.ToString(); return true; })
                .AddHandler("GET", "/users/{user:name}/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _routerBuilder.AddHandler("GET", "/users/{user:name}/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK)))!;

            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_NO_SUCH_PARSER, "name")));
        }

        [Test]
        public async Task AddPrefix_ShouldConfigureHandlersUnderThePrefix()
        {
            _routerBuilder
                .AddPrefix("/api/*", api => api
                    .AddHandler("GET", "/status/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("ready")
                    }));

            HttpMessageRouter router = _routerBuilder.CreateRouter();

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/api/status"),
                s_services
            );

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/status"),
                s_services
            ))!;

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("ready"));
            Assert.That(ex.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task AddPrefix_ShouldAllowPrefixMiddlewareToWrapChildRoutes()
        {
            _routerBuilder
                .AddPrefix("/api/*", api => api
                    .AddHandler(["GET"], async (_, next) =>
                    {
                        HttpResponseMessage response = await next();
                        response.Headers.Add("X-Prefix", "api");
                        return response;
                    })
                    .AddHandler("GET", "/items/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK)));

            HttpMessageRouter router = _routerBuilder.CreateRouter();

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/api/items"),
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Headers.GetValues("X-Prefix"), Is.EquivalentTo(new[] { "api" }));
        }

        [TestCase("")]
        [TestCase("/")]
        [TestCase("/not-prefix")]
        [TestCase("/not-prefix/")]
        [TestCase("/some/not-prefix")]
        public void CreatePrefix_ShouldThrowOnNonPrefixPattern(string pattern)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.CreatePrefix(pattern))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_NOT_PREFIX));
        }

        [TestCase("/path/{invalid-segment}/*", 6)]
        public void CreatePrefix_ShouldThrowOnInvalidPattern(string pattern, int expectedOffset)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.CreatePrefix(pattern))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, expectedOffset)));
        }

        [Test]
        public void AddPrefix_ShouldBeNullChecked()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddPrefix("/base/*", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("configureRoutes"));
        }

        [Test]
        public void AddPrefix_ShouldReturnTheOriginalBuilder()
        {
            RouterBuilder<HttpMessageRouter, RouterConfig> result = _routerBuilder.AddPrefix("/base/*", _ => { });

            Assert.That(result, Is.SameAs(_routerBuilder));
        }
    }
}
