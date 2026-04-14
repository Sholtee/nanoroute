/********************************************************************************
* RouterBuilderTests.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Properties;

    [TestFixture]
    internal sealed partial class RouterBuilderTests
    {
        private static readonly JsonSerializerOptions s_caseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

        internal sealed class TestRouter(RouterBuilder<TestRouter, RouterConfig> routerBuilder) : Router(routerBuilder, routerBuilder.RouterConfig) { }

        private RouterBuilder<TestRouter, RouterConfig> _routerBuilder = null!;

        private Mock<Func<RouterBuilder<TestRouter, RouterConfig>, TestRouter>> _mockRouterFactory = null!;

        private sealed class TestJsonPayload
        {
            public string Name { get; set; } = null!;
        }

        [SetUp]
        public void Setup()
        {
            _mockRouterFactory = new Mock<Func<RouterBuilder<TestRouter, RouterConfig>, TestRouter>>(MockBehavior.Strict);
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

        [TestCase("")]
        [TestCase("/not-prefix")]
        [TestCase("/some/not-prefix")]
        public void CreatePrefix_ShouldThrowOnNonPrefixPattern(string pattern)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.CreatePrefix(pattern))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_NOT_PREFIX));
        }

        [TestCase("/path/{invalid-segment}/")]
        public void CreatePrefix_ShouldThrowOnInvalidPattern(string pattern)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.CreatePrefix(pattern))!;
            Assert.That(ex.ParamName, Is.EqualTo("definition"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PATTERN));
        }

        [Test]
        public void AddPrefix_ShouldBeNullChecked()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddPrefix("/base/", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("configureRoutes"));
        }

        [Test]
        public void AddPrefix_ShouldReturnTheOriginalBuilder()
        {
            RouterBuilder<TestRouter, RouterConfig> result = _routerBuilder.AddPrefix("/base/", _ => { });

            Assert.That(result, Is.SameAs(_routerBuilder));
        }

        [TestCase("/path/{invalid-segment}")]
        public void AddHandler_ShouldThrowOnInvalidPattern(string pattern)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("definition"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PATTERN));
        }

        [TestCase("/users/~denes")]
        [TestCase("/files/a%20b")]
        [TestCase("/mail/a%40b")]
        public void AddHandler_ShouldAllowUriLiteralCharacters(string pattern)
        {
            Assert.DoesNotThrow(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object));
        }

        [TestCase("//")]
        [TestCase("/path//to")]
        [TestCase("/path//to/")]
        public void RoutePatterns_ShouldNormalizeRepeatedSeparators(string pattern)
        {
            Assert.DoesNotThrow(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object));
        }

        [Test]
        public void AddHandler_ShouldThrowOnInvalidVerb()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddHandler("INVALID", "/path/to/somewhere", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, "INVALID")));
        }


        [Test]
        public void AddHandler_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            RequestHandlerDelegate requestHandler = async (_, _) => new HttpResponseMessage();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler(null!, requestHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler("path", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler((IEnumerable<string>) null!, "path", requestHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler((string) null!, "path", requestHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler(["GET"], null!, requestHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler(["GET"], "path", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler("GET", null!, requestHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler("GET", "path", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));
        });
    }
}
