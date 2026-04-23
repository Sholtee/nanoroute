/********************************************************************************
* RouterBuilderTests.Query.cs                                                   *
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
        public async Task AddQueryBindings_ShouldParseConfiguredParametersIntoTheRequestContext()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddPrefix("/items/", items => items
                    .AddQueryBindings("GET", "", "{filter:str(min=3)}&{page?:int(min=1)}")
                    .AddHandler("GET", "", async (context, _) => new HttpResponseMessage
                    {
                        Content = new StringContent($"{context.Parameters["filter"]}:{context.Parameters["page"]}")
                    }))
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://test.test/items?filter=spikey&page=2")
                },
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("spikey:2"));
        }

        [Test]
        public async Task AddQueryBindings_ShouldSkipMissingOptionalParameters()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddQueryBindings("{filter?:str(min=3)}")
                .AddHandler("GET", "/items", async (context, _) => new HttpResponseMessage
                {
                    Content = new StringContent(context.Parameters.ContainsKey("filter") ? "present" : "missing")
                })
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://test.test/items")
                },
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("missing"));
        }

        [Test]
        public void AddQueryBindings_ShouldRejectMissingRequiredParameters()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddPrefix("/items/", items => items
                    .AddQueryBindings("GET", "", "{filter:str(min=3)}")
                    .AddHandler("GET", "", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK)))
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://test.test/items")
                },
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            ))!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ERRORS_NAME], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_MISSING_PARAMETER, "filter") }));
        }

        [Test]
        public void AddQueryBindings_ShouldThrowOnInvalidParameterNames()
        {
            _routerBuilder.AddDefaultValueParsers();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _routerBuilder.AddQueryBindings("{filter-value:str}"))!;

            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, 0)));
        }

        [Test]
        public void AddQueryBindings_ShouldThrowOnMissingValueParsers()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _routerBuilder.AddQueryBindings("{filter:missing}"))!;

            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_NO_SUCH_PARSER, "missing")));
        }

        [Test]
        public async Task AddQueryBindings_ShouldMatchDecodedQueryParameterNames()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddQueryBindings("{query_filter:str(min=3)}")
                .AddHandler("GET", "/items", async (context, _) => new HttpResponseMessage
                {
                    Content = new StringContent((string) context.Parameters["query_filter"]!)
                })
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://test.test/items?query%5Ffilter=spikey")
                },
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            );

            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("spikey"));
        }

        [Test]
        public async Task AddQueryBindings_ShouldHonorConfiguredVerbs()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddQueryBindings("GET", "/items", "{filter:str(min=3)}")
                .AddHandler("GET", "/items", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .AddHandler("POST", "/items", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://test.test/items")
                },
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            ))!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://test.test/items")
                },
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public void QueryHelpers_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).AddQueryBindings(""))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddQueryBindings((string) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("bindings"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddQueryBindings((string) null!, ""))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddQueryBindings("/", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("bindings"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddQueryBindings((IEnumerable<string>) null!, ""))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddQueryBindings((IEnumerable<string>) null!, "/", ""))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddQueryBindings((string) null!, "/", ""))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddQueryBindings("GET", null!, ""))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddQueryBindings("GET", "/", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("bindings"));
        });
    }
}
