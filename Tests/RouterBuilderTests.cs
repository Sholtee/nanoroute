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
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;
    using Json;
    using Properties;

    [TestFixture]
    internal sealed class RouterBuilderTests
    {
        internal sealed class TestRouter(RouterBuilder<TestRouter, RouterConfig> routerBuilder) : Router(routerBuilder, routerBuilder.RouterConfig) { } 

        private RouterBuilder<TestRouter, RouterConfig> _routerBuilder = null!;

        private Mock<Func<RouterConfig, TestRouter>> _mockConfigureRouter = null!;

        private sealed class TestJsonPayload
        {
            public string Name { get; set; } = null!;
        }

        [SetUp]
        public void Setup()
        {
            _mockConfigureRouter = new Mock<Func<RouterConfig, TestRouter>>(MockBehavior.Strict);
            _mockConfigureRouter
                .Setup(c => c.Invoke(It.IsAny<RouterConfig>()))
                .Returns<RouterConfig>(_ => new TestRouter { MatchingBehavior = MatchingBehavior.LiteralFirst });

            _routerBuilder = new RouterBuilder<TestRouter, RouterConfig>(_mockConfigureRouter.Object);
        }

        [Test]
        public void CreateRouter_ShouldCopyTheRootNode()
        {
            Mock<RequestHandler> mockRequestHandler = new(MockBehavior.Strict);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "", mockRequestHandler.Object)
                .CreateRouter();

            Assert.That(router.Root, Is.Not.SameAs(_routerBuilder.Root));
            Assert.That(router.Root.HandlerRegistrations[HttpVerb.Get], Has.Count.EqualTo(1));
            Assert.That(router.Root.HandlerRegistrations[HttpVerb.Get].Single().Handler, Is.SameAs(mockRequestHandler.Object));
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
        public void WithBase_ShouldInheritTheParentParameterParsers()
        {
            RouteBuilder childBuilder = _routerBuilder
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
            RouteBuilder childBuilder = _routerBuilder
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
        public void Patterns_ShouldReflectTheActualBranch()
        {
            _routerBuilder
                .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                .AddHandler("GET", "/", new Mock<RequestHandler>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/somewhere/", new Mock<RequestHandler>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/{some_str_1:any}/not-prefix", new Mock<RequestHandler>(MockBehavior.Strict).Object);

            RouteBuilder childBuilder = _routerBuilder.WithBase("/path/to/")
                .AddHandler("GET", "", new Mock<RequestHandler>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/{some_str_2:any}/something/", new Mock<RequestHandler>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/explicit/something/", new Mock<RequestHandler>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/not-prefix", new Mock<RequestHandler>(MockBehavior.Strict).Object);

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
            RequestHandler handler = async (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

            _routerBuilder
                .AddHandler("GET", "/items", handler)
                .AddHandler("GET", "/items", handler);

            Assert.That(_routerBuilder.Patterns, Is.EqualTo(new[]
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
            RouterBuilder<TestRouter, RouterConfig> result = _routerBuilder.WithBase("/base/", _ => { });

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
        public void AddHandler_ShouldAllowUriLiteralCharacters([Values("/users/~denes", "/files/a%20b", "/mail/a%40b")] string pattern)
        {
            Assert.DoesNotThrow(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestHandler>(MockBehavior.Strict).Object));
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

        [Test]
        public async Task DefaultHandler_ShouldHandleNotFoundEvents([Values] bool populateErrorInfo)
        {
            TestRouter router = _routerBuilder
                .AddDefaultHandler(populateErrorInfo)
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test") };
            request.Properties[Router.TRACE_ID_NAME] = "trace-1";

            HttpResponseMessage response = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            string resp = await response.Content.ReadAsStringAsync();

            ErrorDetails deserialized = JsonSerializer.Deserialize<ErrorDetails>(resp, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Status, Is.EqualTo((int) HttpStatusCode.NotFound));
            Assert.That(deserialized.Title, Is.EqualTo(Resources.ERR_NOT_FOUND));
            Assert.That(deserialized.TraceId, Is.EqualTo("trace-1"));
            Assert.That(deserialized.Errors, Is.Null);
            Assert.That(deserialized.DeveloperMessage, Is.Null);
        }

        [Test]
        public async Task DefaultHandler_ShouldHandleInternalErrors([Values] bool populateErrorInfo)
        {
            const string ERROR_MSG = "Oooops";

            TestRouter router = _routerBuilder
                .AddDefaultHandler(populateErrorInfo)
                .AddHandler("GET", "/somewhere", (_, _) => throw new Exception(ERROR_MSG))
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test/somewhere") };
            request.Properties[Router.TRACE_ID_NAME] = "trace-2";

            HttpResponseMessage response = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));

            string resp = await response.Content.ReadAsStringAsync();

            ErrorDetails deserialized = JsonSerializer.Deserialize<ErrorDetails>(resp, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Status, Is.EqualTo((int) HttpStatusCode.InternalServerError));
            Assert.That(deserialized.Title, Is.EqualTo(Resources.ERR_INERNAL_ERROR));
            Assert.That(deserialized.TraceId, Is.EqualTo("trace-2"));
            Assert.That(deserialized.Errors, Is.Null);
            Assert.That(deserialized.DeveloperMessage, populateErrorInfo ? Has.Some.Contains(ERROR_MSG) : Is.Null);
        }

        [Test]
        public async Task DefaultHandler_ShouldHandleCancellationErrors()
        {
            TestRouter router = _routerBuilder
                .AddDefaultHandler()
                .AddHandler("GET", "/somewhere", (context, _) =>
                {
                    context.Cancellation.ThrowIfCancellationRequested();
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                })
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test/somewhere") };
            request.Properties[Router.TRACE_ID_NAME] = "trace-cancel";

            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            HttpResponseMessage response = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object, cancellation.Token);
            string resp = await response.Content.ReadAsStringAsync();

            ErrorDetails deserialized = JsonSerializer.Deserialize<ErrorDetails>(resp, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestTimeout));
            Assert.That(deserialized.Status, Is.EqualTo((int) HttpStatusCode.RequestTimeout));
            Assert.That(deserialized.Title, Is.EqualTo(Resources.ERR_REQUEST_TIMED_OUT));
            Assert.That(deserialized.TraceId, Is.EqualTo("trace-cancel"));
            Assert.That(deserialized.Errors, Is.Null);
            Assert.That(deserialized.DeveloperMessage, Is.Null);
        }

        [Test]
        public async Task DefaultHandler_ShouldHandleTimeoutErrors()
        {
            TestRouter router = _routerBuilder
                .AddDefaultHandler()
                .AddHandler("GET", "/somewhere", (_, _) => throw new TimeoutException())
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test/somewhere") };
            request.Properties[Router.TRACE_ID_NAME] = "trace-timeout";

            HttpResponseMessage response = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object);
            string resp = await response.Content.ReadAsStringAsync();

            ErrorDetails deserialized = JsonSerializer.Deserialize<ErrorDetails>(resp, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestTimeout));
            Assert.That(deserialized.Status, Is.EqualTo((int) HttpStatusCode.RequestTimeout));
            Assert.That(deserialized.Title, Is.EqualTo(Resources.ERR_REQUEST_TIMED_OUT));
            Assert.That(deserialized.TraceId, Is.EqualTo("trace-timeout"));
            Assert.That(deserialized.Errors, Is.Null);
            Assert.That(deserialized.DeveloperMessage, Is.Null);
        }

        [Test]
        public async Task DefaultHandler_ShouldHandleAggregateExceptions([Values] bool populateErrorInfo)
        {
            AggregateException aggregate = new
            (
                new InvalidOperationException("first problem"),
                new ArgumentException("second problem")
            );

            TestRouter router = _routerBuilder
                .AddDefaultHandler(populateErrorInfo)
                .AddHandler("GET", "/somewhere", (_, _) => throw aggregate)
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test/somewhere") };
            request.Properties[Router.TRACE_ID_NAME] = "trace-aggregate";

            HttpResponseMessage response = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object);
            string resp = await response.Content.ReadAsStringAsync();

            ErrorDetails deserialized = JsonSerializer.Deserialize<ErrorDetails>(resp, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(deserialized.Status, Is.EqualTo((int) HttpStatusCode.InternalServerError));
            Assert.That(deserialized.Title, Is.EqualTo(Resources.ERR_INERNAL_ERROR));
            Assert.That(deserialized.TraceId, Is.EqualTo("trace-aggregate"));
            if (populateErrorInfo)
            {
                Assert.That(deserialized.DeveloperMessage, Has.Exactly(2).Items);
                Assert.That(deserialized.DeveloperMessage, Has.Some.Contains("first problem"));
                Assert.That(deserialized.DeveloperMessage, Has.Some.Contains("second problem"));
            }
            else
                Assert.That(deserialized.DeveloperMessage, Is.Null);
        }

        [Test]
        public async Task DefaultHandler_ShouldEscapeSpecialCharactersInErrorInfo()
        {
            const string ERROR_MSG = "Bad \"quote\"\r\nnext line";

            TestRouter router = _routerBuilder
                .AddDefaultHandler(populateErrorInfo: true)
                .AddHandler("GET", "/somewhere", (_, _) => throw new Exception(ERROR_MSG))
                .CreateRouter();

            HttpRequestMessage request = new() { Method = HttpMethod.Get, RequestUri = new Uri("https://test.test/somewhere") };
            request.Properties[Router.TRACE_ID_NAME] = "trace-3";

            HttpResponseMessage response = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object);
            string resp = await response.Content.ReadAsStringAsync();

            ErrorDetails deserialized = JsonSerializer.Deserialize<ErrorDetails>(resp, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.DeveloperMessage, Has.Some.Contains(ERROR_MSG));
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
        public async Task AddJsonBody_ShouldDeserializeTheRequestBodyIntoTheConfiguredParameter()
        {
            TestJsonPayload? body = null;

            TestRouter router = _routerBuilder
                .AddJsonBody(typeof(TestJsonPayload), "payload", "POST")
                .AddHandler("POST", "/items", async (context, _) =>
                {
                    body = (TestJsonPayload) context.Parameters["payload"]!;
                    return new HttpResponseMessage(HttpStatusCode.Created);
                })
                .CreateRouter();

            HttpRequestMessage request = new(HttpMethod.Post, "https://test.test/items")
            {
                Content = new StringContent("{\"name\":\"Spikey\"}", Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Name, Is.EqualTo("Spikey"));
        }

        [Test]
        public void AddJsonBody_ShouldRejectRequestsWithoutContent()
        {
            TestRouter router = _routerBuilder
                .AddJsonBody(typeof(TestJsonPayload), "payload", "POST")
                .AddHandler("POST", "/items", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .CreateRouter();

            HttpRequestMessage request = new(HttpMethod.Post, "https://test.test/items");

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_METHOD_NOT_ALLOWED));
            Assert.That(ex.Data[HttpRequestExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            Assert.That(ex.Data[HttpRequestExceptionExtensions.ERRORS_NAME], Is.Null);
        }

        [Test]
        public void AddJsonBody_ShouldRejectNonJsonContentTypes()
        {
            TestRouter router = _routerBuilder
                .AddJsonBody(typeof(TestJsonPayload), "payload", "POST")
                .AddHandler("POST", "/items", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .CreateRouter();

            HttpRequestMessage request = new(HttpMethod.Post, "https://test.test/items")
            {
                Content = new StringContent("Spikey", Encoding.UTF8, "text/plain")
            };

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[HttpRequestExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[HttpRequestExceptionExtensions.ERRORS_NAME], Is.EquivalentTo(new[] { Resources.ERR_BAD_CONTENT_TYPE }));
        }

        [Test]
        public void AddJsonBody_ShouldRejectInvalidJsonPayloads()
        {
            TestRouter router = _routerBuilder
                .AddJsonBody(typeof(TestJsonPayload), "payload", "POST")
                .AddHandler("POST", "/items", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .CreateRouter();

            HttpRequestMessage request = new(HttpMethod.Post, "https://test.test/items")
            {
                Content = new StringContent("{", Encoding.UTF8, "application/json")
            };

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[HttpRequestExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[HttpRequestExceptionExtensions.ERRORS_NAME], Is.Not.Null);
        }

        [Test]
        public async Task HttpResponseMessageJson_ShouldSerializeUsingTheProvidedTypeInfo()
        {
            HttpResponseMessage response = HttpResponseMessage.Json(HttpStatusCode.Accepted, new TestJsonPayload { Name = "Spikey" });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
            Assert.That(response.Content, Is.Not.Null);
            Assert.That(response.Content.Headers.ContentType, Is.Not.Null);
            Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
            Assert.That(response.Content.Headers.ContentType!.CharSet, Is.EqualTo("utf-8"));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("{\"name\":\"Spikey\"}"));
        }

        [Test]
        public async Task HttpResponseMessageJson_ShouldRespectSerializerOptions()
        {
            HttpResponseMessage response = HttpResponseMessage.Json
            (
                HttpStatusCode.OK,
                new { PropertyOfAnonObject = 1986 },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper }
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("{\"PROPERTY_OF_ANON_OBJECT\":1986}"));
        }

        [Test]
        public async Task HttpResponseMessageJson_ShouldUseOkAsTheDefaultStatusCode()
        {
            HttpResponseMessage response = HttpResponseMessage.Json(new TestJsonPayload { Name = "Spikey" });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("{\"name\":\"Spikey\"}"));
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
        public void JsonHelpers_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).AddJsonBody(typeof(TestJsonPayload), "payload", "POST"))!;
            Assert.That(ex.ParamName, Is.EqualTo("routerBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddJsonBody(typeInfo: null!, "payload", "POST"))!;
            Assert.That(ex.ParamName, Is.EqualTo("typeInfo"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddJsonBody(type: null!, "payload", "POST"))!;
            Assert.That(ex.ParamName, Is.EqualTo("type"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddJsonBody(typeof(TestJsonPayload), null!, "POST"))!;
            Assert.That(ex.ParamName, Is.EqualTo("paramName"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddJsonBody(typeof(TestJsonPayload), "payload", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => HttpResponseMessage.Json(HttpStatusCode.OK, new TestJsonPayload { Name = "Spikey" }, (JsonTypeInfo) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("typeInfo"));

            ex = Assert.Throws<ArgumentNullException>(() => HttpResponseMessage.Json(HttpStatusCode.OK, new TestJsonPayload { Name = "Spikey" }, (JsonSerializerOptions) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("options"));
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
