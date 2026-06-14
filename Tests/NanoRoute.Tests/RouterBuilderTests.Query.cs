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

using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Properties;

    internal sealed partial class RouterBuilderTests
    {
        [Test]
        public async Task AddQueryBindings_ShouldParseConfiguredParametersIntoTheRequestContext()
        {
            HttpMessageRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddPrefix("/items/*", items => items
                    .AddQueryBindings("GET", RouteScopeBuilder.CurrentExact, "{filter:str(min=3)}&{page?:int(min=1)}")
                    .AddHandler("GET", RouteScopeBuilder.CurrentExact, async (context, _) => new HttpResponseMessage
                    {
                        Content = new StringContent($"{context.Parameters["filter"]}:{context.Parameters["page"]}")
                    }))
                .CreateRouter();

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://test.test/items?filter=spikey&page=2")
                },
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("spikey:2"));
        }

        [Test]
        public async Task WithQueryBindings_ShouldParseConfiguredParametersBeforeEndpointHandler()
        {
            HttpMessageRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddEndpoint("GET", "/items/", endpoint => endpoint
                    .WithQueryBindings("{filter:str(min=3)}&{page?:int(min=1)}")
                    .WithHandler(async (context, _) => new HttpResponseMessage
                    {
                        Content = new StringContent($"{context.Parameters["filter"]}:{context.Parameters["page"]}")
                    }))
                .CreateRouter();

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items?filter=spikey&page=2"),
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("spikey:2"));
        }

        [Test]
        public void WithQueryBindings_ShouldUseEndpointConfiguration()
        {
            HttpMessageRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddEndpoint("GET", "/items/", endpoint => endpoint
                    .WithQueryBindings("{filter:str(min=3)}", unexpected: UnexpectedParameterBehavior.Reject)
                    .WithHandler(async (_, _) => new HttpResponseMessage(HttpStatusCode.OK)))
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items?filter=spikey&unexpected=value"),
                s_services
            ))!;

            Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_UNEXPECTED_PARAMETER, "unexpected") }));
        }

        [Test]
        public async Task AddQueryBindings_ShouldSkipMissingOptionalParameters()
        {
            HttpMessageRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddQueryBindings("{filter?:str(min=3)}")
                .AddHandler("GET", "/items/", async (context, _) => new HttpResponseMessage
                {
                    Content = new StringContent(context.Parameters.ContainsKey("filter") ? "present" : "missing")
                })
                .CreateRouter();

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://test.test/items")
                },
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("missing"));
        }

        [Test]
        public void AddQueryBindings_ShouldRejectMissingRequiredParameters()
        {
            HttpMessageRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddPrefix("/items/*", items => items
                    .AddQueryBindings("GET", RouteScopeBuilder.CurrentExact, "{filter:str(min=3)}")
                    .AddHandler("GET", RouteScopeBuilder.CurrentExact, async (_, _) => new HttpResponseMessage(HttpStatusCode.OK)))
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://test.test/items")
                },
                s_services
            ))!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_MISSING_PARAMETER, "filter") }));
        }

        [Test]
        public void AddQueryBindings_ShouldThrowOnInvalidParameterNames()
        {
            _routerBuilder.AddDefaultValueParsers();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddQueryBindings("{filter-value:str}"))!;

            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, 0)));
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
            HttpMessageRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddQueryBindings("{query_filter:str(min=3)}")
                .AddHandler("GET", "/items/", async (context, _) => new HttpResponseMessage
                {
                    Content = new StringContent((string) context.Parameters["query_filter"]!)
                })
                .CreateRouter();

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://test.test/items?query%5Ffilter=spikey")
                },
                s_services
            );

            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("spikey"));
        }

        [Test]
        public async Task AddQueryBindings_ShouldHonorConfiguredVerbs()
        {
            HttpMessageRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddQueryBindings("GET", "/items/", "{filter:str(min=3)}")
                .AddHandler("GET", "/items/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .AddHandler("POST", "/items/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://test.test/items")
                },
                s_services
            ))!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://test.test/items")
                },
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task AddQueryBindings_ShouldSupportVerbCollectionOverload()
        {
            HttpMessageRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddQueryBindings(new[] { "GET" }, "{filter:str(min=3)}")
                .AddHandler("GET", "/items/", async (context, _) => new HttpResponseMessage
                {
                    Content = new StringContent((string) context.Parameters["filter"]!)
                })
                .CreateRouter();

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items?filter=spikey"),
                s_services
            );

            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("spikey"));
        }

        [Test]
        public async Task AddQueryBindings_ShouldSupportPatternOverload()
        {
            HttpMessageRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddQueryBindings("/items/", "{filter:str(min=3)}")
                .AddHandler("GET", "/items/", async (context, _) => new HttpResponseMessage
                {
                    Content = new StringContent((string) context.Parameters["filter"]!)
                })
                .CreateRouter();

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items?filter=spikey"),
                s_services
            );

            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("spikey"));
        }

        [Test]
        public void AddQueryBindings_ShouldRejectUnexpectedParametersWhenConfigured()
        {
            HttpMessageRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddQueryBindings("GET", "/items/", "{filter:str(min=3)}", unexpected: UnexpectedParameterBehavior.Reject)
                .AddHandler("GET", "/items/", async (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items?filter=spikey&unexpected=value"),
                s_services
            ))!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_UNEXPECTED_PARAMETER, "unexpected") }));
        }

        [Test]
        public async Task AddQueryBindings_ShouldUseRegistrationSpecificUnexpectedBehavior()
        {
            HttpMessageRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddPrefix("/strict/*", strict => strict
                    .AddQueryBindings("GET", RouteScopeBuilder.CurrentExact, "{filter:str(min=3)}", unexpected: UnexpectedParameterBehavior.Reject)
                    .AddHandler("GET", RouteScopeBuilder.CurrentExact, async (_, _) => new HttpResponseMessage(HttpStatusCode.OK)))
                .AddPrefix("/loose/*", loose => loose
                    .AddQueryBindings("GET", RouteScopeBuilder.CurrentExact, "{filter:str(min=3)}", unexpected: UnexpectedParameterBehavior.Ignore)
                    .AddHandler("GET", RouteScopeBuilder.CurrentExact, async (_, _) => new HttpResponseMessage(HttpStatusCode.Accepted)))
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/strict?filter=spikey&unexpected=value"),
                s_services
            ))!;

            Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_UNEXPECTED_PARAMETER, "unexpected") }));

            HttpResponseMessage response = await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/loose?filter=spikey&unexpected=value"),
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        }

        [Test]
        public void QueryHelpers_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            _routerBuilder.AddDefaultValueParsers();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<HttpMessageRouter, RouterConfig>) null!).AddQueryBindings(""))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

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

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddQueryBindings((string) null!, "/", "{filter:str}"))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddQueryBindings("GET", null!, ""))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddQueryBindings("GET", "/", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("bindings"));

            EndpointBuilder endpoint = _routerBuilder.CreateEndpoint("GET", "/items/");

            ex = Assert.Throws<ArgumentNullException>(() => ((EndpointBuilder) null!).WithQueryBindings(""))!;
            Assert.That(ex.ParamName, Is.EqualTo("endpointBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => endpoint.WithQueryBindings(null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("bindings"));
        });

        [Test]
        public void QueryHelpers_ShouldRejectUnknownUnexpectedParameterBehavior() => Assert.Multiple(() =>
        {
            _routerBuilder.AddDefaultValueParsers();

            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => _routerBuilder.AddQueryBindings("{filter:str}", (UnexpectedParameterBehavior) 42))!;
            Assert.That(ex.ParamName, Is.EqualTo("unexpected"));

            EndpointBuilder endpoint = _routerBuilder.CreateEndpoint("GET", "/items/");

            ex = Assert.Throws<ArgumentOutOfRangeException>(() => endpoint.WithQueryBindings("{filter:str}", (UnexpectedParameterBehavior) 42))!;
            Assert.That(ex.ParamName, Is.EqualTo("unexpected"));
        });
    }
}
