/********************************************************************************
* RouterBuilderTests.TypedHandlers.cs                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using HandlerExtensions;
    using Properties;

    internal sealed partial class RouterBuilderTests
    {
        private sealed class GreetingService
        {
            public string Prefix { get; init; } = null!;
        }

        private sealed class TypedRouteRequest
        {
            public int Id { get; set; }
        }

        private sealed class TypedHandlerRequest
        {
            public int Id { get; set; }

            [ValueSource(ValueSource.Context, Name = "query_filter")]
            public string Filter { get; set; } = null!;

            [ValueSource(ValueSource.ServiceLocator)]
            public GreetingService Service { get; set; } = null!;

            public RequestContext RequestContext { get; set; }

            public CancellationToken Cancellation { get; set; }
        }

        private sealed class MissingParameterRequest
        {
            public string Name { get; set; } = null!;
        }

        private sealed class MissingServiceRequest
        {
            [ValueSource(ValueSource.ServiceLocator)]
            public GreetingService Service { get; set; } = null!;
        }

        private sealed class KeyedServiceRequest
        {
            [ValueSource(ValueSource.ServiceLocator, Name = "friendly")]
            public GreetingService Service { get; set; } = null!;
        }

        private sealed class SkippedPropertyRequest
        {
            public int Id { get; set; }

            [ValueSource(ValueSource.Skip)]
            public string OptionalName { get; set; } = "default";
        }

        private sealed class ReadOnlyPropertyRequest
        {
            [ValueSource(ValueSource.Context, Name = "id")]
            public int Id { get; }

            public RequestContext RequestContext { get; set; }
        }

        [Test]
        public async Task AddTypedHandler_ShouldBindContextAndServicesIntoTheRequestObject()
        {
            CancellationToken capturedCancellation = default;
            RequestContext capturedContext = default;

            Mock<IServiceProvider> services = new(MockBehavior.Strict);
            services
                .Setup(s => s.GetService(typeof(GreetingService)))
                .Returns(new GreetingService { Prefix = "hello" });

            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddQueryBindings("GET", "/items/{id:int}", "{query_filter:str(min=3)}")
                .AddHandler
                (
                    ["GET"],
                    "/items/{id:int}",
                    (TypedHandlerRequest request) =>
                    {
                        capturedCancellation = request.Cancellation;
                        capturedContext = request.RequestContext;

                        return Task.FromResult
                        (
                            new HttpResponseMessage(HttpStatusCode.OK)
                            {
                                Content = new StringContent($"{request.Service.Prefix}:{request.Id}:{request.Filter}")
                            }
                        );
                    }
                )
                .CreateRouter();

            using CancellationTokenSource cancellation = new();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/42?query_filter=spikey"),
                services.Object,
                cancellation.Token
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("hello:42:spikey"));
            Assert.That(capturedCancellation, Is.EqualTo(capturedContext.Cancellation));
            Assert.That(capturedContext.Request.RequestUri, Is.EqualTo(new Uri("https://test.test/items/42?query_filter=spikey")));
            Assert.That(capturedContext.Parameters["id"], Is.EqualTo(42));
            Assert.That(capturedContext.Parameters["query_filter"], Is.EqualTo("spikey"));
        }

        [Test]
        public async Task AddTypedHandler_WithInlineQueryBindings_ShouldBindTheConfiguredQueryValues()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler
                (
                    ["GET"],
                    "/items/{id:int}",
                    "{query_filter:str(min=3)}",
                    (TypedHandlerRequest request) => Task.FromResult
                    (
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent($"{request.Id}:{request.Filter}")
                        }
                    )
                )
                .CreateRouter();

            Mock<IServiceProvider> services = new(MockBehavior.Strict);
            services
                .Setup(s => s.GetService(typeof(GreetingService)))
                .Returns(new GreetingService { Prefix = "ignored" });

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/7?query_filter=spikey"),
                services.Object
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("7:spikey"));
        }

        [Test]
        public async Task AddTypedHandler_WithCallNext_ShouldBehaveAsMiddleware()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler
                (
                    ["GET"],
                    "/items/{id:int}",
                    async (TypedRouteRequest request, CallNextHandlerDelegate next) =>
                    {
                        HttpResponseMessage response = await next();
                        response.Headers.Add("X-Route-Id", request.Id.ToString(Resources.Culture));
                        return response;
                    }
                )
                .AddHandler("GET", "/items/{id:int}", (context, _) => Task.FromResult
                (
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(context.Parameters["id"]!.ToString()!)
                    }
                ))
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/42"),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("42"));
            Assert.That(response.Headers.GetValues("X-Route-Id"), Is.EquivalentTo(new[] { "42" }));
        }

        [Test]
        public async Task AddTypedHandler_WithQueryBindingsAndCallNext_ShouldBindValuesBeforeContinuing()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler
                (
                    ["GET"],
                    "/items/{id:int}",
                    "{query_filter:str(min=3)}",
                    async (TypedHandlerRequest request, CallNextHandlerDelegate next) =>
                    {
                        request.RequestContext.Parameters["composed"] = $"{request.Id}:{request.Filter}";
                        return await next();
                    }
                )
                .AddHandler("GET", "/items/{id:int}", (context, _) => Task.FromResult
                (
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent((string) context.Parameters["composed"]!)
                    }
                ))
                .CreateRouter();

            Mock<IServiceProvider> services = new(MockBehavior.Strict);
            services
                .Setup(s => s.GetService(typeof(GreetingService)))
                .Returns(new GreetingService { Prefix = "ignored" });

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/42?query_filter=spikey"),
                services.Object
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("42:spikey"));
        }

        [Test]
        public void AddTypedHandler_ShouldThrowWhenARequiredContextParameterIsMissing()
        {
            TestRouter router = _routerBuilder
                .AddHandler
                (
                    ["GET"],
                    "/items",
                    (MissingParameterRequest _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))
                )
                .CreateRouter();

            InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            ))!;

            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_MISSING_REQUIRED_PARAMETER, "Name")));
        }

        [Test]
        public void AddTypedHandler_ShouldThrowWhenARequiredServiceIsMissing()
        {
            _routerBuilder.AddHandler
            (
                ["GET"],
                "/items",
                (MissingServiceRequest _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))
            );

            TestRouter router = _routerBuilder.CreateRouter();

            Mock<IServiceProvider> services = new(MockBehavior.Strict);
            services
                .Setup(s => s.GetService(typeof(GreetingService)))
                .Returns((object) null!);

            InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                services.Object
            ))!;

            Assert.That(ex.Message, Does.Contain(nameof(GreetingService)));
        }

        [Test]
        public async Task AddTypedHandler_ShouldResolveKeyedServices()
        {
            Mock<IServiceProvider> services = new(MockBehavior.Strict);
            Mock<IKeyedServiceProvider> keyedServices = services.As<IKeyedServiceProvider>();

            keyedServices
                .Setup(s => s.GetRequiredKeyedService(typeof(GreetingService), "friendly"))
                .Returns(new GreetingService { Prefix = "keyed" });

            TestRouter router = _routerBuilder
                .AddHandler
                (
                    ["GET"],
                    "/items",
                    (KeyedServiceRequest request) => Task.FromResult
                    (
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(request.Service.Prefix)
                        }
                    )
                )
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                services.Object
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("keyed"));
        }

        [Test]
        public async Task AddTypedHandler_ShouldLeaveSkippedPropertiesUntouched()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler
                (
                    ["GET"],
                    "/items/{id:int}",
                    (SkippedPropertyRequest request) => Task.FromResult
                    (
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent($"{request.Id}:{request.OptionalName}")
                        }
                    )
                )
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/42"),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("42:default"));
        }

        [Test]
        public void ValueSourceAttribute_ShouldRejectNamesForSkippedProperties()
        {
            Assert.Throws<InvalidOperationException>(() => _ = new ValueSourceAttribute(ValueSource.Skip)
            {
                Name = "ignored"
            });
        }

        [Test]
        public async Task AddTypedHandler_ShouldIgnoreReadOnlyProperties()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler
                (
                    ["GET"],
                    "/items/{id:int}",
                    (ReadOnlyPropertyRequest request) => Task.FromResult
                    (
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent($"{request.Id}:{request.RequestContext.Parameters["id"]}")
                        }
                    )
                )
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/42"),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("0:42"));
        }

        [Test]
        public void TypedHandlerHelpers_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            Func<TypedRouteRequest, Task<HttpResponseMessage>> handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            Func<TypedRouteRequest, CallNextHandlerDelegate, Task<HttpResponseMessage>> middlewareHandler = (_, next) => next();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(((RouterBuilder<TestRouter, RouterConfig>) null!)!, ["GET"], "/items", handler))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(_routerBuilder, null!, "/items", handler))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(_routerBuilder, ["GET"], null!, handler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(_routerBuilder, ["GET"], "/items", (Func<TypedRouteRequest, Task<HttpResponseMessage>>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(_routerBuilder, ["GET"], "/items", (string) null!, handler))!;
            Assert.That(ex.ParamName, Is.EqualTo("queryBindings"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(_routerBuilder, ["GET"], null!, "", handler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(_routerBuilder, ["GET"], "/items", "", (Func<TypedRouteRequest, Task<HttpResponseMessage>>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(((RouterBuilder<TestRouter, RouterConfig>) null!)!, ["GET"], "/items", middlewareHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(_routerBuilder, null!, "/items", middlewareHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(_routerBuilder, ["GET"], null!, middlewareHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(_routerBuilder, ["GET"], "/items", (Func<TypedRouteRequest, CallNextHandlerDelegate, Task<HttpResponseMessage>>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(_routerBuilder, ["GET"], "/items", (string) null!, middlewareHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("queryBindings"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(_routerBuilder, ["GET"], null!, "", middlewareHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteHandlerExtensions.AddHandler<RouterBuilder<TestRouter, RouterConfig>, TypedRouteRequest>(_routerBuilder, ["GET"], "/items", "", (Func<TypedRouteRequest, CallNextHandlerDelegate, Task<HttpResponseMessage>>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));
        });
    }
}
