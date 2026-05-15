/********************************************************************************
* RouterBuilderTests.Handlers.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Properties;

    internal sealed partial class RouterBuilderTests
    {
        [Test]
        public async Task AddHandler_WithSingleVerbAndPattern_ShouldBindHandlerToMatchingRouteAndVerb()
        {
            TestRouter router = _routerBuilder
                .AddHandler("GET", "/items/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("items")
                })
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            );

            HttpRequestException wrongVerb = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Post, "https://test.test/items"),
                s_services
            ))!;

            HttpRequestException wrongPath = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/1"),
                s_services
            ))!;

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("items"));
            Assert.That(wrongVerb.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(wrongPath.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task AddHandler_WithRouteParameters_ShouldPopulateTheRequestContext()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler("GET", "/items/{id:int}/", async (context, _) => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(context.Parameters["id"]!.ToString()!)
                })
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/42"),
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("42"));
        }

        [Test]
        public async Task AddHandler_WithMultipleVerbsAndPattern_ShouldRegisterTheHandlerForEachVerb()
        {
            TestRouter router = _routerBuilder
                .AddHandler(["GET", "POST"], "/items/", async (context, _) => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(context.Request.Method.Method)
                })
                .CreateRouter();

            HttpResponseMessage getResponse = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            );

            HttpResponseMessage postResponse = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Post, "https://test.test/items"),
                s_services
            );

            HttpRequestException deleteResponse = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Delete, "https://test.test/items"),
                s_services
            ))!;

            Assert.That(await getResponse.Content.ReadAsStringAsync(), Is.EqualTo("GET"));
            Assert.That(await postResponse.Content.ReadAsStringAsync(), Is.EqualTo("POST"));
            Assert.That(deleteResponse.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task AddHandler_WithPatternOnlyOverload_ShouldRegisterTheHandlerForAllVerbs()
        {
            TestRouter router = _routerBuilder
                .AddHandler("/items/", async (context, _) => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(context.Request.Method.Method)
                })
                .CreateRouter();

            foreach (string verb in new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "TRACE" })
            {
                HttpResponseMessage response = await router.Handle
                (
                    new HttpRequestMessage(new HttpMethod(verb), "https://test.test/items"),
                    s_services
                );

                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(verb));
            }
        }

        [Test]
        public async Task AddHandler_WithVerbsAndNoPattern_ShouldBindHandlerToTheCurrentBuilderRoot()
        {
            Mock<RequestMiddlewareDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (_, next) => await next());

            TestRouter router = _routerBuilder
                .AddHandler(["GET"], mockHandler.Object)
                .AddHandler("GET", "/items/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .AddHandler("POST", "/items/", async (_, _) => new HttpResponseMessage(HttpStatusCode.Accepted))
                .CreateRouter();

            HttpResponseMessage getResponse = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            );

            HttpResponseMessage postResponse = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Post, "https://test.test/items"),
                s_services
            );

            Assert.Multiple(() =>
            {
                Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(postResponse.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
            });
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [TestCase("/path/{invalid-segment}", 6)]
        public void AddHandler_ShouldThrowOnInvalidPattern(string pattern, int expectedOffset)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestMiddlewareDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, expectedOffset)));
        }

        [TestCase("/users/~denes/")]
        [TestCase("/files/a%20b/")]
        [TestCase("/mail/a%40b/")]
        public void AddHandler_ShouldAllowUriLiteralCharacters(string pattern)
        {
            Assert.DoesNotThrow(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestMiddlewareDelegate>(MockBehavior.Strict).Object));
        }

        [TestCase("//", 1)]
        [TestCase("/path//to", 6)]
        [TestCase("/path//to/", 6)]
        public void AddHandler_ShouldRejectRepeatedSeparators(string pattern, int expectedOffset)
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestMiddlewareDelegate>(MockBehavior.Strict).Object))!;

            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, expectedOffset)));
        }

        [Test]
        public void AddHandler_ShouldThrowOnInvalidVerb()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddHandler("INVALID", "/path/to/somewhere/", new Mock<RequestMiddlewareDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, "INVALID")));
        }

        [Test]
        public async Task WithHandler_ShouldBindRouteValues()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddEndPoint("GET", "/items/{id:int}/", endpoint => endpoint
                    .WithHandler((TypedRouteRequest request) => Task.FromResult
                    (
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(request.Id.ToString(Resources.Culture))
                        }
                    )))
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/42"),
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("42"));
        }

        [Test]
        public async Task WithHandler_ShouldBindRouteValuesBeforeCallingNext()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddEndPoint("GET", "/items/{id:int}/", endpoint => endpoint
                    .WithHandler(async (TypedRouteRequest request, CallNextHandlerDelegate next) =>
                    {
                        HttpResponseMessage response = await next();
                        response.Headers.Add("X-Endpoint-Id", request.Id.ToString(Resources.Culture));
                        return response;
                    })
                    .WithHandler(async (context, _) => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(context.Parameters["id"]!.ToString()!)
                    }))
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/42"),
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("42"));
            Assert.That(response.Headers.GetValues("X-Endpoint-Id"), Is.EquivalentTo(new[] { "42" }));
        }

        [Test]
        public void AddHandler_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            RequestMiddlewareDelegate requestHandler = async (_, _) => new HttpResponseMessage();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler((string) null!, requestHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler("path", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler((IEnumerable<string>) null!, "path", requestHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler((IEnumerable<string>) null!, requestHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler((string) null!, "path", requestHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler(["GET"], null!, requestHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler(["GET"], "path", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler(["GET"], null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler("GET", null!, requestHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler("GET", "path", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));
        });

        [Test]
        public void WithHandler_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            EndPointBuilder endpoint = _routerBuilder.CreateEndPoint("GET", "/items/");
            TypedRequestHandlerDelegate<TypedRouteRequest> typedHandler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            TypedRequestMiddlewareDelegate<TypedRouteRequest> typedMiddleware = (_, next) => next();

            ex = Assert.Throws<ArgumentNullException>(() => ((EndPointBuilder)null!).WithHandler(typedHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("endPointBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => endpoint.WithHandler((TypedRequestHandlerDelegate<TypedRouteRequest>)null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => ((EndPointBuilder)null!).WithHandler(typedMiddleware))!;
            Assert.That(ex.ParamName, Is.EqualTo("endPointBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => endpoint.WithHandler((TypedRequestMiddlewareDelegate<TypedRouteRequest>)null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));
        });
    }
}
