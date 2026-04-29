/********************************************************************************
* RouterBuilderTests.Json.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
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
    using Json;
    using Properties;

    internal sealed partial class RouterBuilderTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task AddJsonErrorDetails_ShouldHandleNotFoundEvents(bool populateErrorInfo)
        {
            TestRouter router = _routerBuilder
                .AddJsonErrorDetails(populateErrorInfo)
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test") };
            request.SetProperty(Router.TRACE_ID_NAME, "trace-1");

            HttpResponseMessage response = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            string resp = await response.Content.ReadAsStringAsync();

            ErrorDetails deserialized = JsonSerializer.Deserialize<ErrorDetails>(resp, s_caseInsensitiveJson)!;
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Status, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(deserialized.Title, Is.EqualTo(Resources.ERR_NOT_FOUND));
            Assert.That(deserialized.TraceId, Is.EqualTo("trace-1"));
            Assert.That(deserialized.Errors, Is.Null);
            Assert.That(deserialized.DeveloperMessage, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task AddJsonErrorDetails_ShouldHandleInternalErrors(bool populateErrorInfo)
        {
            const string ERROR_MSG = "Oooops";

            TestRouter router = _routerBuilder
                .AddJsonErrorDetails(populateErrorInfo)
                .AddHandler("GET", "/somewhere", (_, _) => throw new Exception(ERROR_MSG))
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test/somewhere") };
            request.SetProperty(Router.TRACE_ID_NAME, "trace-2");

            HttpResponseMessage response = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));

            string resp = await response.Content.ReadAsStringAsync();

            ErrorDetails deserialized = JsonSerializer.Deserialize<ErrorDetails>(resp, s_caseInsensitiveJson)!;
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Status, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(deserialized.Title, Is.EqualTo(Resources.ERR_INERNAL_ERROR));
            Assert.That(deserialized.TraceId, Is.EqualTo("trace-2"));
            Assert.That(deserialized.Errors, Is.Null);
            Assert.That(deserialized.DeveloperMessage, populateErrorInfo ? Has.Some.Contains(ERROR_MSG) : Is.Null);
        }

        [Test]
        public async Task AddJsonErrorDetails_ShouldPropagateCancellationErrors()
        {
            TestRouter router = _routerBuilder
                .AddJsonErrorDetails()
                .AddHandler("GET", "/somewhere", (context, _) =>
                {
                    context.Cancellation.ThrowIfCancellationRequested();
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                })
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test/somewhere") };
            request.SetProperty(Router.TRACE_ID_NAME, "trace-cancel");

            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Assert.That(async () => await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object, cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public void AddJsonErrorDetails_ShouldPropagateRouterTimeoutCancellation()
        {
            TestRouter router = _routerBuilder
                .WithConfiguration(config => config.Timeout = TimeSpan.FromMilliseconds(50))
                .AddJsonErrorDetails()
                .AddHandler("GET", "/somewhere", async (context, _) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, context.Cancellation);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                })
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test/somewhere") };

            Assert.That(async () => await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object), Throws.InstanceOf<OperationCanceledException>());
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task AddJsonErrorDetails_ShouldHandleAggregateExceptions(bool populateErrorInfo)
        {
            AggregateException aggregate = new
            (
                new InvalidOperationException("first problem"),
                new ArgumentException("second problem")
            );

            TestRouter router = _routerBuilder
                .AddJsonErrorDetails(populateErrorInfo)
                .AddHandler("GET", "/somewhere", (_, _) => throw aggregate)
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test/somewhere") };
            request.SetProperty(Router.TRACE_ID_NAME, "trace-aggregate");

            HttpResponseMessage response = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Strict).Object);
            string resp = await response.Content.ReadAsStringAsync();

            ErrorDetails deserialized = JsonSerializer.Deserialize<ErrorDetails>(resp, s_caseInsensitiveJson)!;

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(deserialized.Status, Is.EqualTo(HttpStatusCode.InternalServerError));
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
        public async Task AddJsonErrorDetails_ShouldLetNormalWorkflowGo()
        {
            TestRouter router = _routerBuilder
                .AddJsonErrorDetails()
                .AddHandler("GET", "/somewhere", async (_, _) => new HttpResponseMessage { Content = new StringContent("Hello") })
                .CreateRouter();

            HttpResponseMessage response = await router.Handle(new HttpRequestMessage { RequestUri = new Uri("https://test.test/somewhere") }, new Mock<IServiceProvider>(MockBehavior.Strict).Object);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("Hello"));
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

        [TestCase("POST")]
        [TestCase("PUT")]
        public async Task AddJsonBody_ShouldDefaultToPostAndPut(string verb)
        {
            TestJsonPayload? body = null;

            TestRouter router = _routerBuilder
                .AddJsonBody(typeof(TestJsonPayload), "payload")
                .AddHandler(verb, "/items", async (context, _) =>
                {
                    body = (TestJsonPayload) context.Parameters["payload"]!;
                    return new HttpResponseMessage(HttpStatusCode.Created);
                })
                .CreateRouter();

            HttpRequestMessage request = new(new HttpMethod(verb), "https://test.test/items")
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
            Assert.That(ex.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ERRORS_NAME], Is.Null);
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
            Assert.That(ex.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ERRORS_NAME], Is.EquivalentTo(new[] { Resources.ERR_BAD_CONTENT_TYPE }));
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
            Assert.That(ex.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ERRORS_NAME], Is.Not.Null);
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
        public void JsonHelpers_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).AddJsonBody(typeof(TestJsonPayload), "payload", "POST"))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeBuilder"));

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
    }
}
