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

            [ValueSource(ValueSource.Parameter, Name = "query_filter")]
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
            [ValueSource(ValueSource.Parameter, Name = "id")]
            public int Id { get; }

            public RequestContext RequestContext { get; set; }
        }

        private sealed class NamedParameterRequest
        {
            [ValueSource(ValueSource.Parameter, Name = "id")]
            public int ItemId { get; set; }
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
                .AddQueryBindings("GET", "/items/{id:int}/", "{query_filter:str(min=3)}")
                .AddHandler
                (
                    ["GET"],
                    "/items/{id:int}/",
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
        public async Task AddTypedHandler_WithQueryBindings_ShouldBindTheConfiguredQueryValues()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddQueryBindings("GET", "/items/{id:int}/", "{query_filter:str(min=3)}")
                .AddHandler
                (
                    "GET",
                    "/items/{id:int}/",
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
        public async Task AddTypedHandler_WithParameterValueSource_ShouldBindTheConfiguredParameterName()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler
                (
                    "GET",
                    "/items/{id:int}/",
                    (NamedParameterRequest request) => Task.FromResult
                    (
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(request.ItemId.ToString(Resources.Culture))
                        }
                    )
                )
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
        public async Task AddTypedHandler_WithCallNext_ShouldBehaveAsMiddleware()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler
                (
                    ["GET"],
                    "/items/{id:int}/",
                    async (TypedRouteRequest request, CallNextHandlerDelegate next) =>
                    {
                        HttpResponseMessage response = await next();
                        response.Headers.Add("X-Route-Id", request.Id.ToString(Resources.Culture));
                        return response;
                    }
                )
                .AddHandler("GET", "/items/{id:int}/", (context, _) => Task.FromResult
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
                s_services
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
                .AddQueryBindings("GET", "/items/{id:int}/", "{query_filter:str(min=3)}")
                .AddHandler
                (
                    "GET",
                    "/items/{id:int}/",
                    async (TypedHandlerRequest request, CallNextHandlerDelegate next) =>
                    {
                        request.RequestContext.Parameters["composed"] = $"{request.Id}:{request.Filter}";
                        return await next();
                    }
                )
                .AddHandler("GET", "/items/{id:int}/", (context, _) => Task.FromResult
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
        public async Task AddTypedHandler_WithPatternOnlyOverload_ShouldRegisterAllVerbs()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler
                (
                    "/items/{id:int}/",
                    (TypedRouteRequest request) => Task.FromResult
                    (
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(request.Id.ToString(Resources.Culture))
                        }
                    )
                )
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Post, "https://test.test/items/42"),
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("42"));
        }

        [Test]
        public async Task AddTypedHandler_WithSingleVerbMiddlewareOverload_ShouldHonorTheVerb()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler
                (
                    "GET",
                    "/items/{id:int}/",
                    async (TypedRouteRequest request, CallNextHandlerDelegate next) =>
                    {
                        HttpResponseMessage response = await next();
                        response.Headers.Add("X-Typed-Id", request.Id.ToString(Resources.Culture));
                        return response;
                    }
                )
                .AddHandler("GET", "/items/{id:int}/", (context, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))
                .CreateRouter();

            HttpResponseMessage response = await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/42"),
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Headers.GetValues("X-Typed-Id"), Is.EquivalentTo(new[] { "42" }));
        }

        [Test]
        public void AddTypedHandler_ShouldThrowWhenARequiredContextParameterIsMissing()
        {
            TestRouter router = _routerBuilder
                .AddHandler
                (
                    ["GET"],
                    "/items/",
                    (MissingParameterRequest _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))
                )
                .CreateRouter();

            InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_MISSING_REQUIRED_PARAMETER, "Name")));
        }

        [Test]
        public void AddTypedHandler_ShouldThrowWhenARequiredServiceIsMissing()
        {
            _routerBuilder.AddHandler
            (
                ["GET"],
                "/items/",
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
                    "/items/",
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
                    "/items/{id:int}/",
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
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("42:default"));
        }

        [Test]
        public void ValueSourceAttribute_ShouldRejectNamesForSkippedProperties()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _ = new ValueSourceAttribute(ValueSource.Skip)
            {
                Name = "ignored"
            })!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_SKIPPED_VALUE_SOURCE_NAME));
        }

        [Test]
        public async Task AddTypedHandler_ShouldIgnoreReadOnlyProperties()
        {
            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler
                (
                    ["GET"],
                    "/items/{id:int}/",
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
                s_services
            );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("0:42"));
        }

        [Test]
        public void TypedHandlerHelpers_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            TypedRequestEndpointHandlerDelegate<TypedRouteRequest> handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            TypedRequestHandlerDelegate<TypedRouteRequest> pipelineHandler = (_, next) => next();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).AddHandler("/items/", handler))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler(null!, handler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler("/items/", (TypedRequestEndpointHandlerDelegate<TypedRouteRequest>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler((string) null!, "/items/", handler))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler("GET", null!, handler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler("GET", "/items/", (TypedRequestEndpointHandlerDelegate<TypedRouteRequest>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).AddHandler(["GET"], "/items/", handler))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler((IEnumerable<string>) null!, "/items/", handler))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler(["GET"], null!, handler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler(["GET"], "/items/", (TypedRequestEndpointHandlerDelegate<TypedRouteRequest>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).AddHandler("/items/", pipelineHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler(null!, pipelineHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler("/items/", (TypedRequestHandlerDelegate<TypedRouteRequest>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler((string) null!, "/items/", pipelineHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler("GET", null!, pipelineHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler("GET", "/items/", (TypedRequestHandlerDelegate<TypedRouteRequest>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).AddHandler(["GET"], "/items/", pipelineHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler((IEnumerable<string>) null!, "/items/", pipelineHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler(["GET"], null!, pipelineHandler))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddHandler(["GET"], "/items/", (TypedRequestHandlerDelegate<TypedRouteRequest>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));
        });
    }
}
