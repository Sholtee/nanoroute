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

        internal sealed class TestRouter(RouterBuilder<TestRouter, RouterConfig> routerBuilder) : Router(routerBuilder, routerBuilder.RouterConfig) { }

        private RouterBuilder<TestRouter, RouterConfig> _routerBuilder = null!;

        private Mock<RouterFactoryDelegate<TestRouter, RouterConfig>> _mockRouterFactory = null!;

        private sealed class TestJsonPayload
        {
            public string Name { get; set; } = null!;
        }

        [SetUp]
        public void Setup()
        {
            _mockRouterFactory = new Mock<RouterFactoryDelegate<TestRouter, RouterConfig>>(MockBehavior.Strict);
            _routerBuilder = new RouterBuilder<TestRouter, RouterConfig>(_mockRouterFactory.Object);

            _mockRouterFactory
                .Setup(c => c.Invoke(_routerBuilder))
                .Returns<RouterBuilder<TestRouter, RouterConfig>>(bldr => new TestRouter(bldr));
        }

        [Test]
        public void CreateRouter_ShouldPassTheBuilderIntoTheRouterFactory()
        {
            TestRouter router = _routerBuilder.CreateRouter();

            Assert.That(router, Is.Not.Null);
            _mockRouterFactory.Verify(c => c.Invoke(_routerBuilder), Times.Once);
        }

        [Test]
        public async Task CreateRouter_ShouldCreateAnImmutableSnapshot()
        {
            TestRouter router = _routerBuilder
                .AddHandler("GET", "/before", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .CreateRouter();

            _routerBuilder.AddHandler("GET", "/after", async (_, _) => new HttpResponseMessage(HttpStatusCode.Accepted));

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle(new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = new Uri("https://test.test/after") }, new Mock<IServiceProvider>(MockBehavior.Strict).Object))!;
            Assert.That(ex.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public void RoutePatterns_ShouldDeduplicateIdenticalEntries()
        {
            RequestHandlerDelegate handler = async (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

            _routerBuilder
                .AddHandler("GET", "/items", handler)
                .AddHandler("GET", "/items", handler);

            Assert.That(_routerBuilder.Patterns, Is.EqualTo(new[]
            {
                "[Get] /items"
            }));
        }

        [Test]
        public void Patterns_ShouldReflectTheActualBranch()
        {
            _routerBuilder
                .AddValueParser("any", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = segment.ToString(); return true; })
                .AddHandler("GET", "/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/somewhere/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/{some_str_1:any}/not-prefix", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object);

            RouteBuilder childBuilder = _routerBuilder.CreatePrefix("/path/to/")
                .AddHandler("GET", "", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/{some_str_2:any}/something/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/explicit/something/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/not-prefix", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object);

            Assert.That(_routerBuilder.Patterns, Is.EqualTo(new string[]
            {
                "[Get] /",
                "[Get] /{some_str_1:any}/not-prefix",
                "[Get] /path/to/",
                "[Get] /path/to/{some_str_2:any}/something/",
                "[Get] /path/to/explicit/something/",
                "[Get] /path/to/not-prefix",
                "[Get] /somewhere/",
            }));
            Assert.That(childBuilder.Patterns, Is.EqualTo(new string[]
            {
                "[Get] /path/to/",
                "[Get] /path/to/{some_str_2:any}/something/",
                "[Get] /path/to/explicit/something/",
                "[Get] /path/to/not-prefix"
            }));
        }

        [Test]
        public void BasePattern_ShouldReflectTheBuilderBranch()
        {
            RouteBuilder childBuilder = _routerBuilder.CreatePrefix("/path/to/");
            RouteBuilder nestedBuilder = childBuilder.CreatePrefix("/nested/");

            Assert.That(_routerBuilder.BasePattern, Is.Empty);
            Assert.That(childBuilder.BasePattern, Is.EqualTo("/path/to/"));
            Assert.That(nestedBuilder.BasePattern, Is.EqualTo("/path/to/nested/"));
        }
    }
}
