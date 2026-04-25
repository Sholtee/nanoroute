/********************************************************************************
* RouterBuilderTests.Exceptions.cs                                              *
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
    internal sealed partial class RouterBuilderTests
    {
        [Test]
        public async Task AddExceptionHandler_ShouldHonorConfiguredPattern()
        {
            TestRouter router = _routerBuilder
                .AddExceptionHandler("/items")
                .AddHandler("GET", "/items", (_, _) => throw new InvalidOperationException("boom"))
                .AddHandler("GET", "/other", (_, _) => throw new InvalidOperationException("boom"))
                .CreateRouter();

            HttpRequestException handled = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            ))!;

            Assert.That(handled.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.InternalServerError));

            Assert.That(async () => await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/other"),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            ), Throws.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public async Task AddExceptionHandler_ShouldHonorConfiguredVerbAndPattern()
        {
            TestRouter router = _routerBuilder
                .AddExceptionHandler("GET", "/items")
                .AddHandler("GET", "/items", (_, _) => throw new InvalidOperationException("boom"))
                .AddHandler("POST", "/items", (_, _) => throw new InvalidOperationException("boom"))
                .CreateRouter();

            HttpRequestException handled = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            ))!;

            Assert.That(handled.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.InternalServerError));

            Assert.That(async () => await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Post, "https://test.test/items"),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object
            ), Throws.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void ExceptionHelpers_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).AddExceptionHandler())!;
            Assert.That(ex.ParamName, Is.EqualTo("routeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler((string) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler((IReadOnlyCollection<string>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler((IReadOnlyCollection<string>) null!, "/"))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler(["GET"], null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler((string) null!, "/"))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler("GET", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
        });

        [Test]
        public void GetErrorDetails_ShouldAcceptIntegerStatusCodes()
        {
            HttpRequestException ex = new("teapot");
            ex.Data[NanoRouteExceptionExtensions.STATUS_NAME] = 404;

            ErrorDetails details = ex.GetErrorDetails(traceId: "trace");

            Assert.That(details.Status, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(details.Title, Is.EqualTo("teapot"));
            Assert.That(details.TraceId, Is.EqualTo("trace"));
        }

        [Test]
        public void GetErrorDetails_ShouldUseInternalServerErrorWhenStatusCodeIsMissing()
        {
            ErrorDetails details = new HttpRequestException("boom").GetErrorDetails(traceId: "trace");

            Assert.That(details.Status, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(details.Title, Is.EqualTo("boom"));
            Assert.That(details.TraceId, Is.EqualTo("trace"));
        }
    }
}
