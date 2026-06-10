/********************************************************************************
* RouterBuilderTests.Json.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Properties;

    internal sealed partial class RouterBuilderTests
    {
        internal delegate RouterBuilder<TestRouter, RouterConfig> ConfigureJsonBodyDelegate(RouterBuilder<TestRouter, RouterConfig> builder);

        private sealed class TestJsonPayload
        {
            public string Name { get; set; } = null!;
        }

        private static JsonTypeInfo TestJsonPayloadTypeInfo => JsonSerializerOptions.Web.GetTypeInfo(typeof(TestJsonPayload));

        private static IEnumerable<TestCaseData> AddJsonBodyOverloadCases()
        {
            yield return new TestCaseData
            (
                "IEnumerable verbs, pattern, JsonTypeInfo",
                (ConfigureJsonBodyDelegate) (builder => builder.AddJsonBody(["POST"], "/items/", TestJsonPayloadTypeInfo, "payload"))
            );
            yield return new TestCaseData
            (
                "string verb, pattern, JsonTypeInfo",
                (ConfigureJsonBodyDelegate) (builder => builder.AddJsonBody("POST", "/items/", TestJsonPayloadTypeInfo, "payload"))
            );
            yield return new TestCaseData
            (
                "IEnumerable verbs, JsonTypeInfo",
                (ConfigureJsonBodyDelegate) (builder => builder.AddJsonBody(["POST"], TestJsonPayloadTypeInfo, "payload"))
            );
            yield return new TestCaseData
            (
                "pattern, JsonTypeInfo",
                (ConfigureJsonBodyDelegate) (builder => builder.AddJsonBody("/items/", TestJsonPayloadTypeInfo, "payload"))
            );
            yield return new TestCaseData
            (
                "JsonTypeInfo",
                (ConfigureJsonBodyDelegate) (builder => builder.AddJsonBody(TestJsonPayloadTypeInfo, "payload"))
            );
            yield return new TestCaseData
            (
                "IEnumerable verbs, pattern, Type",
                (ConfigureJsonBodyDelegate) (builder => builder.AddJsonBody(["POST"], "/items/", typeof(TestJsonPayload), "payload"))
            );
            yield return new TestCaseData
            (
                "string verb, pattern, Type",
                (ConfigureJsonBodyDelegate) (builder => builder.AddJsonBody("POST", "/items/", typeof(TestJsonPayload), "payload"))
            );
            yield return new TestCaseData
            (
                "IEnumerable verbs, Type",
                (ConfigureJsonBodyDelegate) (builder => builder.AddJsonBody(["POST"], typeof(TestJsonPayload), "payload"))
            );
            yield return new TestCaseData
            (
                "pattern, Type",
                (ConfigureJsonBodyDelegate) (builder => builder.AddJsonBody("/items/", typeof(TestJsonPayload), "payload"))
            );
            yield return new TestCaseData
            (
                "Type",
                (ConfigureJsonBodyDelegate) (builder => builder.AddJsonBody(typeof(TestJsonPayload), "payload"))
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task AddJsonErrorDetails_ShouldHandleNotFoundEvents(bool populateErrorInfo)
        {
            TestRouter router = _routerBuilder
                .ConfigureJsonErrorDetails(config => config with
                {
                    PopulateErrorInfo = populateErrorInfo
                })
                .AddJsonErrorDetails()
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test") };
            request.TraceId = "trace-1";

            HttpResponseMessage response = await router.Handle(request, s_services);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            string resp = await response.Content.ReadAsStringAsync();

            ErrorDetails deserialized = JsonSerializer.Deserialize<ErrorDetails>(resp, s_caseInsensitiveJson)!;
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Status, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(deserialized.Title, Is.EqualTo(Resources.ERR_NOT_FOUND));
            Assert.That(deserialized.TraceId, Is.EqualTo("trace-1"));
            Assert.That(deserialized.Errors, Is.Null);
            Assert.That(deserialized.DeveloperMessages, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task AddJsonErrorDetails_ShouldHandleInternalErrors(bool populateErrorInfo)
        {
            const string ERROR_MSG = "Oooops";

            TestRouter router = _routerBuilder
                .ConfigureJsonErrorDetails(config => config with
                {
                    PopulateErrorInfo = populateErrorInfo
                })
                .AddJsonErrorDetails()
                .AddHandler("GET", "/somewhere/", (_, _) => throw new Exception(ERROR_MSG))
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test/somewhere") };
            request.TraceId = "trace-2";

            HttpResponseMessage response = await router.Handle(request, s_services);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));

            string resp = await response.Content.ReadAsStringAsync();

            ErrorDetails deserialized = JsonSerializer.Deserialize<ErrorDetails>(resp, s_caseInsensitiveJson)!;
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Status, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(deserialized.Title, Is.EqualTo(Resources.ERR_INTERNAL_ERROR));
            Assert.That(deserialized.TraceId, Is.EqualTo("trace-2"));
            Assert.That(deserialized.Errors, Is.Null);
            Assert.That(deserialized.DeveloperMessages, populateErrorInfo ? Has.Some.Contains(ERROR_MSG) : Is.Null);
        }

        [Test]
        public async Task AddJsonErrorDetails_ShouldPropagateCancellationErrors()
        {
            TestRouter router = _routerBuilder
                .AddJsonErrorDetails()
                .AddHandler("GET", "/somewhere/", (context, _) =>
                {
                    context.Cancellation.ThrowIfCancellationRequested();
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                })
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test/somewhere") };
            request.TraceId = "trace-cancel";

            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Assert.That(async () => await router.Handle(request, s_services, cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public void AddJsonErrorDetails_ShouldPropagateCancellationDuringHandlerExecution()
        {
            using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(50));

            TestRouter router = _routerBuilder
                .AddJsonErrorDetails()
                .AddHandler("GET", "/somewhere/", async (context, _) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, context.Cancellation);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                })
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test/somewhere") };

            Assert.That(async () => await router.Handle(request, s_services, cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
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
                .ConfigureJsonErrorDetails(config => config with
                {
                    PopulateErrorInfo = populateErrorInfo
                })
                .AddJsonErrorDetails()
                .AddHandler("GET", "/somewhere/", (_, _) => throw aggregate)
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test/somewhere") };
            request.TraceId = "trace-aggregate";

            HttpResponseMessage response = await router.Handle(request, s_services);
            string resp = await response.Content.ReadAsStringAsync();

            ErrorDetails deserialized = JsonSerializer.Deserialize<ErrorDetails>(resp, s_caseInsensitiveJson)!;

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(deserialized.Status, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(deserialized.Title, Is.EqualTo(Resources.ERR_INTERNAL_ERROR));
            Assert.That(deserialized.TraceId, Is.EqualTo("trace-aggregate"));
            if (populateErrorInfo)
            {
                Assert.That(deserialized.DeveloperMessages, Has.Exactly(2).Items);
                Assert.That(deserialized.DeveloperMessages, Has.Some.Contains("first problem"));
                Assert.That(deserialized.DeveloperMessages, Has.Some.Contains("second problem"));
            }
            else
                Assert.That(deserialized.DeveloperMessages, Is.Null);
        }

        [Test]
        public async Task AddJsonErrorDetails_ShouldLetNormalWorkflowGo()
        {
            TestRouter router = _routerBuilder
                .AddJsonErrorDetails()
                .AddHandler("GET", "/somewhere/", async (_, _) => new HttpResponseMessage { Content = new StringContent("Hello") })
                .CreateRouter();

            HttpResponseMessage response = await router.Handle(new HttpRequestMessage { RequestUri = new Uri("https://test.test/somewhere") }, s_services);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("Hello"));
        }

        [Test]
        public async Task ConfigureJsonErrorDetails_ShouldConfigureErrorDetailsSerialization()
        {
            TestRouter router = _routerBuilder
                .ConfigureJsonErrorDetails(config => config with
                {
                    ErrorDetailsTypeInfo = SnakeCaseErrorDetailsJsonContext.Default.ErrorDetails
                })
                .AddJsonErrorDetails()
                .CreateRouter();

            HttpRequestMessage request = new() { RequestUri = new Uri("https://test.test") };
            request.TraceId = "trace-snake";

            HttpResponseMessage response = await router.Handle(request, s_services);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("\"trace_id\":\"trace-snake\""));
        }

        [Test]
        public async Task AddJsonErrorDetails_ShouldHonorConfiguredVerbsAndPattern()
        {
            TestRouter router = _routerBuilder
                .AddJsonErrorDetails("GET", "/api/*")
                .AddHandler("GET", "/api/fail/", (_, _) => throw new InvalidOperationException("handled"))
                .AddHandler("POST", "/api/fail/", (_, _) => throw new InvalidOperationException("unhandled verb"))
                .AddHandler("GET", "/other/fail/", (_, _) => throw new InvalidOperationException("unhandled path"))
                .CreateRouter();

            HttpResponseMessage response = await router.Handle(new HttpRequestMessage(HttpMethod.Get, "https://test.test/api/fail"), s_services);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(async () => await router.Handle(new HttpRequestMessage(HttpMethod.Post, "https://test.test/api/fail"), s_services), Throws.InstanceOf<InvalidOperationException>());
            Assert.That(async () => await router.Handle(new HttpRequestMessage(HttpMethod.Get, "https://test.test/other/fail"), s_services), Throws.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public async Task AddJsonErrorDetails_ShouldHonorConfiguredVerbs()
        {
            TestRouter router = _routerBuilder
                .AddJsonErrorDetails(new[] { "POST" })
                .AddHandler("POST", "/fail/", (_, _) => throw new InvalidOperationException("handled"))
                .AddHandler("GET", "/fail/", (_, _) => throw new InvalidOperationException("unhandled verb"))
                .CreateRouter();

            HttpResponseMessage response = await router.Handle(new HttpRequestMessage(HttpMethod.Post, "https://test.test/fail"), s_services);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(async () => await router.Handle(new HttpRequestMessage(HttpMethod.Get, "https://test.test/fail"), s_services), Throws.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public async Task AddJsonBody_ShouldDeserializeTheRequestBodyIntoTheConfiguredParameter()
        {
            TestJsonPayload? body = null;

            TestRouter router = _routerBuilder
                .AddJsonBody("POST", RouteScopeBuilder.CurrentPrefix, typeof(TestJsonPayload), "payload")
                .AddHandler("POST", "/items/", async (context, _) =>
                {
                    body = (TestJsonPayload) context.Parameters["payload"]!;
                    return new HttpResponseMessage(HttpStatusCode.Created);
                })
                .CreateRouter();

            HttpRequestMessage request = new(HttpMethod.Post, "https://test.test/items")
            {
                Content = new StringContent("{\"name\":\"Spikey\"}", Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await router.Handle(request, s_services);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Name, Is.EqualTo("Spikey"));
        }

        [TestCaseSource(nameof(AddJsonBodyOverloadCases))]
        public async Task AddJsonBody_OverloadsShouldDeserializeTheRequestBodyIntoTheConfiguredParameter(string _, ConfigureJsonBodyDelegate configureJsonBody)
        {
            TestJsonPayload? body = null;

            TestRouter router = configureJsonBody(_routerBuilder)
                .AddHandler("POST", "/items/", async (context, _) =>
                {
                    body = (TestJsonPayload) context.Parameters["payload"]!;
                    return new HttpResponseMessage(HttpStatusCode.Created);
                })
                .CreateRouter();

            HttpRequestMessage request = new(HttpMethod.Post, "https://test.test/items")
            {
                Content = new StringContent("{\"name\":\"Spikey\"}", Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await router.Handle(request, s_services);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Name, Is.EqualTo("Spikey"));
        }

        [TestCase("POST")]
        [TestCase("PUT")]
        [TestCase("PATCH")]
        public async Task AddJsonBody_ShouldDefaultToVerbsHavingBody(string verb)
        {
            TestJsonPayload? body = null;

            TestRouter router = _routerBuilder
                .AddJsonBody(typeof(TestJsonPayload), "payload")
                .AddHandler(verb, "/items/", async (context, _) =>
                {
                    body = (TestJsonPayload) context.Parameters["payload"]!;
                    return new HttpResponseMessage(HttpStatusCode.Created);
                })
                .CreateRouter();

            HttpRequestMessage request = new(new HttpMethod(verb), "https://test.test/items")
            {
                Content = new StringContent("{\"name\":\"Spikey\"}", Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await router.Handle(request, s_services);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Name, Is.EqualTo("Spikey"));
        }

        [Test]
        public void AddJsonBody_ShouldRejectRequestsWithoutContent()
        {
            TestRouter router = _routerBuilder
                .AddJsonBody("POST", RouteScopeBuilder.CurrentPrefix, typeof(TestJsonPayload), "payload")
                .AddHandler("POST", "/items/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .CreateRouter();

            HttpRequestMessage request = new(HttpMethod.Post, "https://test.test/items");

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle(request, s_services))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.EquivalentTo(new string[] { Resources.ERR_MISSING_BODY }));
        }

        [Test]
        public void AddJsonBody_ShouldRejectNonJsonContentTypes()
        {
            TestRouter router = _routerBuilder
                .AddJsonBody("POST", RouteScopeBuilder.CurrentPrefix, typeof(TestJsonPayload), "payload")
                .AddHandler("POST", "/items/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .CreateRouter();

            HttpRequestMessage request = new(HttpMethod.Post, "https://test.test/items")
            {
                Content = new StringContent("Spikey", Encoding.UTF8, "text/plain")
            };

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle(request, s_services))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.EquivalentTo(new[] { Resources.ERR_BAD_CONTENT_TYPE }));
        }

        [Test]
        public void AddJsonBody_ShouldRejectInvalidJsonPayloads()
        {
            TestRouter router = _routerBuilder
                .AddJsonBody("POST", RouteScopeBuilder.CurrentPrefix, typeof(TestJsonPayload), "payload")
                .AddHandler("POST", "/items/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .CreateRouter();

            HttpRequestMessage request = new(HttpMethod.Post, "https://test.test/items")
            {
                Content = new StringContent("{", Encoding.UTF8, "application/json")
            };

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle(request, s_services))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.Not.Null);
        }

        [Test]
        public async Task WithJsonBody_ShouldDeserializeBodyBeforeEndpointHandler()
        {
            TestJsonPayload? payload = null;

            TestRouter router = _routerBuilder
                .AddEndpoint("POST", "/items/", endpoint => endpoint
                    .WithJsonBody<TestJsonPayload>("payload")
                    .WithHandler(async (context, _) =>
                    {
                        payload = (TestJsonPayload) context.Parameters["payload"]!;
                        return new HttpResponseMessage(HttpStatusCode.Created);
                    }))
                .CreateRouter();

            HttpRequestMessage request = new(HttpMethod.Post, "https://test.test/items")
            {
                Content = new StringContent("{\"name\":\"Spikey\"}", Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await router.Handle(request, s_services);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload!.Name, Is.EqualTo("Spikey"));
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
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).AddJsonBody("POST", "/", typeof(TestJsonPayload), "payload"))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddJsonBody("POST", "/", typeInfo: null!, "payload"))!;
            Assert.That(ex.ParamName, Is.EqualTo("typeInfo"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddJsonBody("POST", "/", type: null!, "payload"))!;
            Assert.That(ex.ParamName, Is.EqualTo("type"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddJsonBody("POST", "/", typeof(TestJsonPayload), null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("paramName"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddJsonBody((IEnumerable<string>) null!, "/", typeof(TestJsonPayload), "payload"))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => HttpResponseMessage.Json(HttpStatusCode.OK, new TestJsonPayload { Name = "Spikey" }, (JsonTypeInfo) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("typeInfo"));

            ex = Assert.Throws<ArgumentNullException>(() => HttpResponseMessage.Json(HttpStatusCode.OK, new TestJsonPayload { Name = "Spikey" }, (JsonSerializerOptions) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("options"));

            ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).ConfigureJsonErrorDetails(config => config))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.ConfigureJsonErrorDetails(null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("configure"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.ConfigureJsonErrorDetails(_ => null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("config"));

            ex = Assert.Throws<ArgumentNullException>(() => new JsonErrorDetailsConfig { ErrorDetailsTypeInfo = null! })!;
            Assert.That(ex.ParamName, Is.EqualTo("value"));

            EndpointBuilder endpoint = _routerBuilder.CreateEndpoint("GET", "/items/");

            ex = Assert.Throws<ArgumentNullException>(() => endpoint.WithJsonBody((Type) null!, "payload"))!;
            Assert.That(ex.ParamName, Is.EqualTo("type"));

            ex = Assert.Throws<ArgumentNullException>(() => endpoint.WithJsonBody<TestJsonPayload>(null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("paramName"));
        });
    }

    [JsonSerializable(typeof(ErrorDetails))]
    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    internal partial class SnakeCaseErrorDetailsJsonContext : JsonSerializerContext
    {
    }
}
