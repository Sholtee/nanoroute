/********************************************************************************
* RouterBuilderTests.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;
    using Properties;


    [TestFixture]
    internal sealed class RouterBuilderTests
    {
        internal sealed class TestRouter : Router { } 

        private RouterBuilder<TestRouter> _routerBuilder = null!;

        private Mock<Action<TestRouter>> _mockConfigureRouter = null!;

        [SetUp]
        public void Setup()
        {
            _mockConfigureRouter = new Mock<Action<TestRouter>>(MockBehavior.Strict);
            _mockConfigureRouter
                .Setup(c => c.Invoke(It.IsAny<TestRouter>()));

            _routerBuilder = new RouterBuilder<TestRouter>(_mockConfigureRouter.Object);
        }

        [Test]
        public void CreateRouter_ShouldCopyTheRootNode()
        {
            _mockConfigureRouter
                .Setup(s => s.Invoke(It.IsAny<TestRouter>()));

            Mock<RequestHandler> mockRequestHandler = new(MockBehavior.Strict);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "", mockRequestHandler.Object)
                .CreateRouter();

            Assert.That(router.Root, Is.Not.SameAs(_routerBuilder.Root));
            Assert.That(router.Root.HandlerRegistrations[HttpVerb.Get], Has.Count.EqualTo(1));
            Assert.That(router.Root.HandlerRegistrations[HttpVerb.Get].Single().Handler, Is.SameAs(mockRequestHandler.Object));
        }

        [Test]
        public void CreateRouter_ShouldInvokeTheConfigureCallbackOnTheCreatedRouter()
        {
            TestRouter? configuredRouter = null;

            _mockConfigureRouter
                .Setup(c => c.Invoke(It.IsAny<TestRouter>()))
                .Callback<TestRouter>(router => configuredRouter = router);

            TestRouter router = _routerBuilder.CreateRouter();

            _mockConfigureRouter.Verify(c => c.Invoke(It.IsAny<TestRouter>()), Times.Once);
            Assert.That(configuredRouter, Is.SameAs(router));
        }

        [Test]
        public async Task CreateRouter_ShouldCreateAnImmutableSnapshot()
        {
            TestRouter router = _routerBuilder
                .AddHandler("GET", "/before", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .CreateRouter();

            _routerBuilder.AddHandler("GET", "/after", async (_, _) => new HttpResponseMessage(HttpStatusCode.Accepted));

            HttpException ex = Assert.ThrowsAsync<HttpException>(() => router.Handle(new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = new Uri("https://test.test/after") }, new Mock<IServiceProvider>(MockBehavior.Strict).Object))!;
            Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public void WithBase_ShouldInheritTheParentParameterParsers()
        {
            RouterBuilder<TestRouter> childBuilder = _routerBuilder
                .AddParameterParser("str", (string segment, out object? parsed) => { parsed = segment; return true; })
                .WithBase("/to/")
                .AddParameterParser("int", (string segment, out object? parsed) => { parsed = segment; return true; });


            Assert.DoesNotThrow(() => childBuilder.AddHandler("/{str}/{int}", new Mock<RequestHandler>(MockBehavior.Strict).Object));
        }

        [Test]
        public void WithBase_ShouldCreateAnIndependentParserScope()
        {
            _routerBuilder
                .AddParameterParser("str", (string segment, out object? parsed) => { parsed = segment; return true; })
                .WithBase("/to/")
                .AddParameterParser("int", (string segment, out object? parsed) => { parsed = segment; return true; });

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _routerBuilder.AddHandler("/{str}/{int}", new Mock<RequestHandler>(MockBehavior.Strict).Object))!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_NO_SUCH_PARAMETER_PARSER, "int")));
        }

        [Test]
        public async Task AddParameterParser_ShouldReplaceExistingParserRegistrations()
        {
            TestRouter router = _routerBuilder
                .AddParameterParser("value", (string segment, out object? parsed) => { parsed = $"first:{segment}"; return true; })
                .AddParameterParser("value", (string segment, out object? parsed) => { parsed = $"second:{segment}"; return true; })
                .AddHandler("GET", "/items/{id:value}", async (context, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(context.Parameters["id"]!.ToString()!) })
                .CreateRouter();

            HttpResponseMessage response = await router.Handle(new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = new Uri("https://test.test/items/42") }, new Mock<IServiceProvider>(MockBehavior.Strict).Object);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("second:42"));
        }

        [Test]
        public async Task WithBase_ShouldKeepChildParserOverridesLocal()
        {
            RouterBuilder<TestRouter> childBuilder = _routerBuilder
                .AddParameterParser("value", (string segment, out object? parsed) => { parsed = $"parent:{segment}"; return true; })
                .WithBase("/child/")
                .AddParameterParser("value", (string segment, out object? parsed) => { parsed = $"child:{segment}"; return true; });

            RequestHandler handler = async (context, _) => new HttpResponseMessage { Content = new StringContent(context.Parameters["id"]!.ToString()!) };

            childBuilder
                .AddHandler("GET", "/{id:value}", handler);

            _routerBuilder
                .AddHandler("GET", "/{id:value}", handler);

            TestRouter router = _routerBuilder.CreateRouter();

            HttpResponseMessage
                parentResponse = await router.Handle(new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = new Uri("https://test.test/42") }, new Mock<IServiceProvider>(MockBehavior.Strict).Object),
                childResponse = await router.Handle(new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = new Uri("https://test.test/child/42") }, new Mock<IServiceProvider>(MockBehavior.Strict).Object);

            Assert.That(await parentResponse.Content.ReadAsStringAsync(), Is.EqualTo("parent:42"));
            Assert.That(await childResponse.Content.ReadAsStringAsync(), Is.EqualTo("child:42"));
        }

        [Test]
        public void RoutePatterns_ShouldReflectTheActualBranch()
        {
            _routerBuilder
                .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                .AddHandler("GET", "/", new Mock<RequestHandler>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/somewhere/", new Mock<RequestHandler>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/{some_str_1:any}/not-prefix", new Mock<RequestHandler>(MockBehavior.Strict).Object);

            RouterBuilder<TestRouter> childBuilder = _routerBuilder.WithBase("/path/to/")
                .AddHandler("GET", "", new Mock<RequestHandler>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/{some_str_2:any}/something/", new Mock<RequestHandler>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/explicit/something/", new Mock<RequestHandler>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/not-prefix", new Mock<RequestHandler>(MockBehavior.Strict).Object);

            Assert.That(_routerBuilder.RoutePatterns, Is.EqualTo(new string[]
            {
                "[Get] /",
                "[Get] /{some_str_1:any}/not-prefix",
                "[Get] /path/to/",
                "[Get] /path/to/{some_str_2:any}/something/",
                "[Get] /path/to/explicit/something/",
                "[Get] /path/to/not-prefix",
                "[Get] /somewhere/",
            }));
            Assert.That(childBuilder.RoutePatterns, Is.EqualTo(new string[]
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
            RequestHandler handler = async (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

            _routerBuilder
                .AddHandler("GET", "/items", handler)
                .AddHandler("GET", "/items", handler);

            Assert.That(_routerBuilder.RoutePatterns, Is.EqualTo(new[]
            {
                "[Get] /items"
            }));
        }

        [Test]
        public void WithBase_ShouldThrowOnNonPrefixPattern([Values("", "/not-prefix", "/some/not-prefix")] string pattern)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.WithBase(pattern))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_NOT_PREFIX));
        }

        [Test]
        public void WithBase_ShouldThrowOnInvalidPattern([Values("//", "/path//to/", "/path/{invalid-segment}/")] string pattern)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.WithBase(pattern))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PATTERN));
        }

        [Test]
        public void WithBase_WithConfigureRoutes_ShouldBeNullChecked()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.WithBase("/base/", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("configureRoutes"));
        }

        [Test]
        public void WithBase_WithConfigureRoutes_ShouldReturnTheOriginalBuilder()
        {
            RouterBuilder<TestRouter> result = _routerBuilder.WithBase("/base/", _ => { });

            Assert.That(result, Is.SameAs(_routerBuilder));
        }

        [Test]
        public void AddHandler_ShouldThrowOnInvalidPattern([Values("//", "/path//to", "/path/{invalid-segment}")] string pattern)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestHandler>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PATTERN));
        }

        [Test]
        public void AddHandler_ShouldThrowOnMissingParameterParser([Values("path/to/{missing}", "path/to/{parameter_name:missing}")] string pattern)
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _routerBuilder.AddHandler(pattern, new Mock<RequestHandler>(MockBehavior.Strict).Object))!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_NO_SUCH_PARAMETER_PARSER, "missing")));
        }


        [Test]
        public void AddHandler_ShouldThrowOnInvalidVerb()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddHandler("INVALID", "/path/to/somewhere", new Mock<RequestHandler>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, "INVALID")));
        }
        
        [Test]
        public void AddHandler_ShouldThrowOnParameterOverride()
        {
            _routerBuilder
                .AddParameterParser("int", new Mock<ParameterParserDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/path/to/{id:int}", new Mock<RequestHandler>(MockBehavior.Strict).Object);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _routerBuilder.AddHandler("GET", "/path/to/{other_id:int}", new Mock<RequestHandler>(MockBehavior.Strict).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_PARAMETER_OVERRIDE));
        }

        private sealed class JsonResponse
        {
            public required string Message { get; set; }

            public string? Reason { get; set; }
        }

        [Test]
        public async Task DefaultHandler_ShouldHandleNotFoundEvents([Values] bool populateErrorInfo)
        {
            TestRouter router = _routerBuilder
                .AddDefaultHandler(populateErrorInfo)
                .CreateRouter();

            HttpResponseMessage response = await router.Handle(new HttpRequestMessage { RequestUri = new Uri("https://test.test") }, new Mock<IServiceProvider>(MockBehavior.Strict).Object);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            string resp = await response.Content.ReadAsStringAsync();

            JsonResponse deserialized = JsonSerializer.Deserialize<JsonResponse>(resp, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Reason, Is.Null);
            Assert.That(deserialized.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
        }

        [Test]
        public async Task DefaultHandler_ShouldHandleInternalErrors([Values] bool populateErrorInfo)
        {
            const string ERROR_MSG = "Oooops";

            TestRouter router = _routerBuilder
                .AddDefaultHandler(populateErrorInfo)
                .AddHandler("GET", "/somewhere", (_, _) => throw new Exception(ERROR_MSG))
                .CreateRouter();

            HttpResponseMessage response = await router.Handle(new HttpRequestMessage { RequestUri = new Uri("https://test.test/somewhere") }, new Mock<IServiceProvider>(MockBehavior.Strict).Object);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));

            string resp = await response.Content.ReadAsStringAsync();

            JsonResponse deserialized = JsonSerializer.Deserialize<JsonResponse>(resp, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Reason, populateErrorInfo ? Does.Contain(ERROR_MSG) : Is.Null);
            Assert.That(deserialized.Message, Is.EqualTo(Resources.ERR_INERNAL_ERROR));
        }

        [Test]
        public async Task DefaultHandler_ShouldEscapeSpecialCharactersInErrorInfo()
        {
            const string ERROR_MSG = "Bad \"quote\"\r\nnext line";

            TestRouter router = _routerBuilder
                .AddDefaultHandler(populateErrorInfo: true)
                .AddHandler("GET", "/somewhere", (_, _) => throw new Exception(ERROR_MSG))
                .CreateRouter();

            HttpResponseMessage response = await router.Handle(new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = new Uri("https://test.test/somewhere") }, new Mock<IServiceProvider>(MockBehavior.Strict).Object);
            string resp = await response.Content.ReadAsStringAsync();

            JsonResponse deserialized = JsonSerializer.Deserialize<JsonResponse>(resp, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Reason, Does.Contain(ERROR_MSG));
        }
        
        [Test]
        public async Task DefaultHandler_ShouldLetNormalWorkflowGo()
        {
            TestRouter router = _routerBuilder
                .AddDefaultHandler()
                .AddHandler("GET", "/somewhere", async (_, _) => new HttpResponseMessage { Content = new StringContent("Hello") })
                .CreateRouter();

            HttpResponseMessage response = await router.Handle(new HttpRequestMessage { RequestUri = new Uri("https://test.test/somewhere") }, new Mock<IServiceProvider>(MockBehavior.Strict).Object);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("Hello"));
        }

        private static IEnumerable<TestCaseData> AddDefaultParsers_ShouldRegisterTheBuiltInParsers_Cases()
        {
            yield return new TestCaseData("int", 42);
            yield return new TestCaseData("guid", Guid.Parse("4a91f2c0-0e3c-4ec8-9f8c-8d2d2f2c7d1a"));
            yield return new TestCaseData("bool", true);
            yield return new TestCaseData("str", "spikey");
        }

        [TestCaseSource(nameof(AddDefaultParsers_ShouldRegisterTheBuiltInParsers_Cases))]
        public async Task AddDefaultParsers_ShouldRegisterTheBuiltInParsers(string parserName, object expectedValue)
        {
            TestRouter router = _routerBuilder
                .AddDefaultParsers()
                .AddHandler("GET", $"/items/{{value:{parserName}}}", async (context, _) => new HttpResponseMessage { Content = new StringContent(context.Parameters["value"]!.ToString()!) })
                .CreateRouter();

            HttpResponseMessage response = await router.Handle(new HttpRequestMessage { RequestUri = new Uri($"https://test.test/items/{expectedValue}") }, new Mock<IServiceProvider>(MockBehavior.Strict).Object);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(expectedValue.ToString()));
        }
       
        [Test]
        public void AddParameterParser_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddParameterParser(null!, new Mock<ParameterParserDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("parserName"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddParameterParser("any", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("tryParseDelegate"));
        });

        [Test]
        public void AddHandler_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            RequestHandler requestHandler = async (_, _) => new HttpResponseMessage();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler(null!, requestHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler("path", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler((IEnumerable<string>)null!, "path", requestHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler((string)null!, "path", requestHandler))!;
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
