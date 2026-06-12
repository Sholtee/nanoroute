/********************************************************************************
* RouterBuilderTests.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    [TestFixture]
    internal sealed partial class RouterBuilderTests
    {
        private static readonly JsonSerializerOptions s_caseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

        private static readonly IServiceProvider s_services = new Mock<IServiceProvider>(MockBehavior.Strict).Object;

        private RouterBuilder<HttpMessageRouter, RouterConfig> _routerBuilder = null!;

        private sealed class TestRouter(RouteScopeBuilder routes, RouterConfig config) : RouterBase<RouterConfig>(routes, config);

        [SetUp]
        public void Setup() => _routerBuilder = HttpMessageRouter.CreateBuilder();

        [Test]
        public void CurrentRoutePatternConstants_ShouldExposeExistingRouteSemantics()
        {
            Assert.That(RouteScopeBuilder.CurrentExact, Is.EqualTo("/"));
            Assert.That(RouteScopeBuilder.CurrentPrefix, Is.EqualTo("/*"));
        }

        [Test]
        public void CreateRouter_ShouldPassTheBuilderIntoTheRouterFactory()
        {
            TestRouter expectedRouter = new(new RouteScopeBuilder(), new RouterConfig());
            RouterBuilder<TestRouter, RouterConfig>? factoryArgument = null;
            RouterBuilder<TestRouter, RouterConfig> routerBuilder = new(builder =>
            {
                factoryArgument = builder;
                return expectedRouter;
            });

            TestRouter router = routerBuilder.CreateRouter();

            Assert.That(router, Is.SameAs(expectedRouter));
            Assert.That(factoryArgument, Is.SameAs(routerBuilder));
        }

        [Test]
        public void CreateRouter_ShouldCreateAnImmutableSnapshot()
        {
            HttpMessageRouter router = _routerBuilder
                .AddHandler("GET", "/before/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .CreateRouter();

            _routerBuilder.AddHandler("GET", "/after/", async (_, _) => new HttpResponseMessage(HttpStatusCode.Accepted));

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route(new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = new Uri("https://test.test/after") }, s_services))!;
            Assert.That(ex.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public void RoutePatterns_ShouldDeduplicateIdenticalEntries()
        {
            RequestHandlerDelegate handler = async (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

            _routerBuilder
                .AddHandler("GET", "/items/", handler)
                .AddHandler("GET", "/items/", handler);

            Assert.That(_routerBuilder.Patterns, Is.EqualTo(new[]
            {
                "[Get] /items/"
            }));
        }

        [Test]
        public void Patterns_ShouldReflectTheActualBranch()
        {
            _routerBuilder
                .AddValueParser("any", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = segment.ToString(); return true; })
                .AddHandler("GET", RouteScopeBuilder.CurrentPrefix, new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/somewhere/*", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/{some_str_1:any}/not-prefix/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object);

            RouteScopeBuilder childBuilder = _routerBuilder.CreatePrefix("/path/to/*")
                .AddHandler("GET", RouteScopeBuilder.CurrentExact, new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/{some_str_2:any}/something/*", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/explicit/something/*", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/not-prefix/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object);

            Assert.That(_routerBuilder.Patterns, Is.EqualTo(new string[]
            {
                "[Get] /*",
                "[Get] /path/to/",
                "[Get] /path/to/explicit/something/*",
                "[Get] /path/to/not-prefix/",
                "[Get] /path/to/{some_str_2:any}/something/*",
                "[Get] /somewhere/*",
                "[Get] /{some_str_1:any}/not-prefix/",
            }));
            Assert.That(childBuilder.Patterns, Is.EqualTo(new string[]
            {
                "[Get] /path/to/",
                "[Get] /path/to/explicit/something/*",
                "[Get] /path/to/not-prefix/",
                "[Get] /path/to/{some_str_2:any}/something/*",
            }));
        }

        [Test]
        public void BasePattern_ShouldReflectTheBuilderBranch()
        {
            RouteScopeBuilder childBuilder = _routerBuilder.CreatePrefix("/path/to/*");
            RouteScopeBuilder nestedBuilder = childBuilder.CreatePrefix("/nested/*");

            Assert.That(_routerBuilder.BasePattern, Is.EqualTo("/*"));
            Assert.That(childBuilder.BasePattern, Is.EqualTo("/path/to/*"));
            Assert.That(nestedBuilder.BasePattern, Is.EqualTo("/path/to/nested/*"));
        }
    }
}
