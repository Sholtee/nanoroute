/********************************************************************************
* RouterBuilderTests.EndPoints.cs                                               *
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
    using Properties;

    internal sealed partial class RouterBuilderTests
    {
        private sealed record EndpointMetadata(string Value);

        [Test]
        public async Task AddEndPoint_WithExactPattern_ShouldMatchOnlyExactPath()
        {
            TestRouter router = _routerBuilder
                .AddEndPoint("GET", "/items/", endpoint => endpoint
                    .WithHandler(async (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("exact")
                    }))
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            );

            HttpRequestException wrongPath = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/42"),
                s_services
            ))!;

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("exact"));
            Assert.That(wrongPath.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task AddEndPoint_WithPrefixPattern_ShouldMatchNestedPaths()
        {
            TestRouter router = _routerBuilder
                .AddEndPoint("GET", "/files/*", endpoint => endpoint
                    .WithHandler(async (context, _) => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(context.Request.RequestUri!.AbsolutePath)
                    }))
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/files/path/to/file"),
                s_services
            );

            HttpRequestException wrongPath = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/other/path/to/file"),
                s_services
            ))!;

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("/files/path/to/file"));
            Assert.That(wrongPath.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task AddEndPoint_WithMultipleVerbs_ShouldApplyEndpointHandlersToEachVerb()
        {
            TestRouter router = _routerBuilder
                .AddEndPoint(["GET", "POST"], "/items/", endpoint => endpoint
                    .WithHandler(async (context, _) => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(context.Request.Method.Method)
                    }))
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
            Assert.That(deleteResponse.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task AddEndPoint_WithMultipleHandlers_ShouldInvokeHandlersInRegistrationOrder()
        {
            List<string> calls = [];

            TestRouter router = _routerBuilder
                .AddEndPoint("GET", "/items/", endpoint => endpoint
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

            HttpResponseMessage response = await router.Handle
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
        public async Task CreateEndPoint_ShouldAllowDeferredEndpointConfiguration()
        {
            EndPointBuilder endpoint = _routerBuilder.CreateEndPoint("GET", "/deferred/");

            endpoint.WithHandler(async (_, _) => new HttpResponseMessage(HttpStatusCode.Accepted));

            TestRouter router = _routerBuilder.CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/deferred"),
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        }

        [Test]
        public void EndPointBuilder_Metadata_ShouldExposeEndpointScopedMetadata()
        {
            _routerBuilder.Metadata.Set(new EndpointMetadata("parent"));

            EndPointBuilder endpoint = _routerBuilder.CreateEndPoint("GET", "/items/");

            Assert.That(endpoint.Metadata.GetOrDefault(new EndpointMetadata("missing")), Is.EqualTo(new EndpointMetadata("parent")));

            endpoint.Metadata.Set(new EndpointMetadata("endpoint"));

            Assert.Multiple(() =>
            {
                Assert.That(endpoint.Metadata.GetOrDefault(new EndpointMetadata("missing")), Is.EqualTo(new EndpointMetadata("endpoint")));
                Assert.That(_routerBuilder.Metadata.GetOrDefault(new EndpointMetadata("missing")), Is.EqualTo(new EndpointMetadata("parent")));
            });
        }

        [Test]
        public void EndPointHelpers_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            RequestHandlerDelegate handler = async (_, _) => new HttpResponseMessage(HttpStatusCode.OK);
            EndPointBuilder endpoint = _routerBuilder.CreateEndPoint("GET", "/items/");

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).CreateEndPoint("GET", "/items/"))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.CreateEndPoint((IEnumerable<string>) null!, "/items/"))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.CreateEndPoint(["GET"], null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.CreateEndPoint((string) null!, "/items/"))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));

            ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).AddEndPoint(["GET"], "/items/", _ => { }))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddEndPoint((IEnumerable<string>) null!, "/items/", _ => { }))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddEndPoint(["GET"], null!, _ => { }))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddEndPoint(["GET"], "/items/", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("configureEndPoint"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddEndPoint((string) null!, "/items/", _ => { }))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));

            ex = Assert.Throws<ArgumentNullException>(() => endpoint.WithHandler(null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => ((EndPointBuilder) null!).WithJsonBody<TestJsonPayload>("payload"))!;
            Assert.That(ex.ParamName, Is.EqualTo("endPointBuilder"));
        });

        [TestCase("")]
        [TestCase("items")]
        [TestCase("/items")]
        public void CreateEndPoint_ShouldThrowOnInvalidPattern(string pattern)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.CreateEndPoint("GET", pattern))!;

            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith("Invalid pattern"));
        }

        [Test]
        public void AddEndPoint_ShouldReturnTheOriginalBuilder()
        {
            RouterBuilder<TestRouter, RouterConfig> result = _routerBuilder.AddEndPoint("GET", "/items/", _ => { });

            Assert.That(result, Is.SameAs(_routerBuilder));
        }
    }
}
