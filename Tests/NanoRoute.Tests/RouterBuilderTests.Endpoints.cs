/********************************************************************************
* RouterBuilderTests.Endpoints.cs                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    internal sealed partial class RouterBuilderTests
    {
        [Test]
        public async Task AddEndpoint_WithExactPattern_ShouldMatchOnlyExactPath()
        {
            HttpMessageRouter router = _routerBuilder
                .AddEndpoint("GET", "/items/", Endpoint => Endpoint
                    .WithHandler(async (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("exact")
                    }))
                .CreateRouter();

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            );

            HttpRequestException wrongPath = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/42"),
                s_services
            ))!;

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("exact"));
            Assert.That(wrongPath.Status, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task AddEndpoint_WithPrefixPattern_ShouldMatchNestedPaths()
        {
            HttpMessageRouter router = _routerBuilder
                .AddEndpoint("GET", "/files/*", Endpoint => Endpoint
                    .WithHandler(async (context, _) => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(context.Request.RequestUri!.AbsolutePath)
                    }))
                .CreateRouter();

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/files/path/to/file"),
                s_services
            );

            HttpRequestException wrongPath = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/other/path/to/file"),
                s_services
            ))!;

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("/files/path/to/file"));
            Assert.That(wrongPath.Status, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task AddEndpoint_WithMultipleVerbs_ShouldApplyEndpointHandlersToEachVerb()
        {
            HttpMessageRouter router = _routerBuilder
                .AddEndpoint(["GET", "POST"], "/items/", Endpoint => Endpoint
                    .WithHandler(async (context, _) => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(context.Request.Method.Method)
                    }))
                .CreateRouter();

            HttpResponseMessage getResponse = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            );

            HttpResponseMessage postResponse = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Post, "https://test.test/items"),
                s_services
            );

            HttpRequestException deleteResponse = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Delete, "https://test.test/items"),
                s_services
            ))!;

            Assert.That(await getResponse.Content.ReadAsStringAsync(), Is.EqualTo("GET"));
            Assert.That(await postResponse.Content.ReadAsStringAsync(), Is.EqualTo("POST"));
            Assert.That(deleteResponse.Status, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task AddEndpoint_WithMultipleHandlers_ShouldInvokeHandlersInRegistrationOrder()
        {
            List<string> calls = [];

            HttpMessageRouter router = _routerBuilder
                .AddEndpoint("GET", "/items/", Endpoint => Endpoint
                    .WithHandler(async (_, next) =>
                    {
                        calls.Add("first");
                        HttpResponseMessage response = await next();
                        response.Headers.Add("X-Endpoint", "first");
                        return response;
                    })
                    .WithHandler(async (_, _) =>
                    {
                        calls.Add("second");
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("handled")
                        };
                    }))
                .CreateRouter();

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("handled"));
            Assert.That(response.Headers.GetValues("X-Endpoint"), Is.EquivalentTo(new[] { "first" }));
            Assert.That(calls, Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public async Task CreateEndpoint_ShouldAllowDeferredEndpointConfiguration()
        {
            EndpointBuilder endpoint = _routerBuilder.CreateEndpoint("GET", "/deferred/");

            endpoint.WithHandler(async (_, _) => new HttpResponseMessage(HttpStatusCode.Accepted));

            HttpMessageRouter router = _routerBuilder.CreateRouter();

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/deferred"),
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        }

        [Test]
        public void EndpointHelpers_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            RequestHandlerDelegate handler = async (_, _) => new HttpResponseMessage(HttpStatusCode.OK);
            EndpointBuilder endpoint = _routerBuilder.CreateEndpoint("GET", "/items/");

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<HttpMessageRouter, RouterConfig>) null!).CreateEndpoint("GET", "/items/"))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.CreateEndpoint((IEnumerable<string>) null!, "/items/"))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.CreateEndpoint(["GET"], null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.CreateEndpoint((string) null!, "/items/"))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));

            ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<HttpMessageRouter, RouterConfig>) null!).AddEndpoint(["GET"], "/items/", _ => { }))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddEndpoint((IEnumerable<string>) null!, "/items/", _ => { }))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddEndpoint(["GET"], null!, _ => { }))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddEndpoint(["GET"], "/items/", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("configureEndpoint"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddEndpoint((string) null!, "/items/", _ => { }))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));

            ex = Assert.Throws<ArgumentNullException>(() => endpoint.WithHandler(null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => ((EndpointBuilder) null!).WithJsonBody<TestJsonPayload>("payload"))!;
            Assert.That(ex.ParamName, Is.EqualTo("endpointBuilder"));
        });

        [TestCase("")]
        [TestCase("items")]
        [TestCase("/items")]
        public void CreateEndpoint_ShouldThrowOnInvalidPattern(string pattern)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.CreateEndpoint("GET", pattern))!;

            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith("Invalid pattern"));
        }

        [Test]
        public void AddEndpoint_ShouldReturnTheOriginalBuilder()
        {
            RouterBuilder<HttpMessageRouter, RouterConfig> result = _routerBuilder.AddEndpoint("GET", "/items/", _ => { });

            Assert.That(result, Is.SameAs(_routerBuilder));
        }
    }
}
