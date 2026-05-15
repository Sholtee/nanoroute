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

using NUnit.Framework;

namespace NanoRoute.Tests
{
    internal sealed partial class RouterBuilderTests
    {
        private static HttpRequestException NormalizeConflict(Exception ex)
        {
            HttpRequestException normalized = new("conflict", ex);
            normalized.Data[NanoRouteExceptionExtensions.StatusName] = HttpStatusCode.Conflict;
            normalized.Data[NanoRouteExceptionExtensions.ErrorsName] = new[] { "custom-error" };
            normalized.Data[NanoRouteExceptionExtensions.DeveloperMessagesName] = new[] { "custom-developer-message" };
            return normalized;
        }

        private static HttpRequestException NormalizeTeapot(Exception ex)
        {
            HttpRequestException normalized = new("teapot", ex);
            normalized.Data[NanoRouteExceptionExtensions.StatusName] = (HttpStatusCode) 418;
            return normalized;
        }

        [Test]
        public async Task AddExceptionHandler_ShouldHonorConfiguredPattern()
        {
            TestRouter router = _routerBuilder
                .AddExceptionHandler("/items/")
                .AddHandler("GET", "/items/", (_, _) => throw new InvalidOperationException("boom"))
                .AddHandler("GET", "/other/", (_, _) => throw new InvalidOperationException("boom"))
                .CreateRouter();

            HttpRequestException handled = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.That(handled.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.InternalServerError));

            Assert.That(async () => await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/other"),
                s_services
            ), Throws.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public async Task AddExceptionHandler_ShouldHonorConfiguredVerbAndPattern()
        {
            TestRouter router = _routerBuilder
                .AddExceptionHandler("GET", "/items/")
                .AddHandler("GET", "/items/", (_, _) => throw new InvalidOperationException("boom"))
                .AddHandler("POST", "/items/", (_, _) => throw new InvalidOperationException("boom"))
                .CreateRouter();

            HttpRequestException handled = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.That(handled.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.InternalServerError));

            Assert.That(async () => await router.Handle
            (
                new HttpRequestMessage(HttpMethod.Post, "https://test.test/items"),
                s_services
            ), Throws.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void AddExceptionHandler_ShouldUseConfiguredExceptionNormalizers()
        {
            TestRouter router = _routerBuilder
                .ConfigureExceptionHandling(config => config with
                {
                    ExceptionNormalizers = config.ExceptionNormalizers.SetItem(typeof(NotSupportedException), NormalizeConflict)
                })
                .AddExceptionHandler()
                .AddHandler("GET", "/items/", (_, _) => throw new NotSupportedException("nope"))
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.Multiple(() =>
            {
                Assert.That(ex.Message, Is.EqualTo("conflict"));
                Assert.That(ex.InnerException, Is.InstanceOf<NotSupportedException>());
                Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.Conflict));
                Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.EquivalentTo(new[] { "custom-error" }));
                Assert.That(ex.Data[NanoRouteExceptionExtensions.DeveloperMessagesName], Is.EquivalentTo(new[] { "custom-developer-message" }));
            });
        }

        [Test]
        public void AddExceptionHandler_ShouldSnapshotExceptionHandlingConfiguration()
        {
            TestRouter router = _routerBuilder
                .ConfigureExceptionHandling(config => config with
                {
                    ExceptionNormalizers = config.ExceptionNormalizers.SetItem(typeof(NotSupportedException), NormalizeConflict)
                })
                .AddExceptionHandler()
                .ConfigureExceptionHandling(config => config with
                {
                    ExceptionNormalizers = config.ExceptionNormalizers.SetItem(typeof(NotSupportedException), NormalizeTeapot)
                })
                .AddHandler("GET", "/items/", (_, _) => throw new NotSupportedException("nope"))
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.Conflict));
        }

        [Test]
        public void ConfigureExceptionHandling_ShouldUseScopedBuilderMetadata()
        {
            _routerBuilder
                .ConfigureExceptionHandling(config => config with
                {
                    ExceptionNormalizers = config.ExceptionNormalizers.SetItem(typeof(NotSupportedException), NormalizeConflict)
                })
                .AddPrefix("/child/*", child => child
                    .ConfigureExceptionHandling(config => config with
                    {
                        ExceptionNormalizers = config.ExceptionNormalizers.SetItem(typeof(NotSupportedException), NormalizeTeapot)
                    })
                    .AddExceptionHandler("/items/")
                    .AddHandler("GET", "/items/", (_, _) => throw new NotSupportedException("child")))
                .AddExceptionHandler("/parent/")
                .AddHandler("GET", "/parent/", (_, _) => throw new NotSupportedException("parent"));

            TestRouter router = _routerBuilder.CreateRouter();

            HttpRequestException
                parentEx = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
                (
                    new HttpRequestMessage(HttpMethod.Get, "https://test.test/parent"),
                    s_services
                ))!,
                childEx = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
                (
                    new HttpRequestMessage(HttpMethod.Get, "https://test.test/child/items"),
                    s_services
                ))!;

            Assert.Multiple(() =>
            {
                Assert.That(parentEx.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.Conflict));
                Assert.That(childEx.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo((HttpStatusCode) 418));
            });
        }

        [Test]
        public void AddExceptionHandler_ShouldKeepDefaultAggregateExceptionNormalization()
        {
            AggregateException aggregate = new
            (
                new InvalidOperationException("first problem"),
                new ArgumentException("second problem")
            );

            TestRouter router = _routerBuilder
                .AddExceptionHandler()
                .AddHandler("GET", "/items/", (_, _) => throw aggregate)
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.Multiple(() =>
            {
                Assert.That(ex.Message, Is.EqualTo(Properties.Resources.ERR_INTERNAL_ERROR));
                Assert.That(ex.InnerException, Is.SameAs(aggregate));
                Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.InternalServerError));
                Assert.That(ex.Data[NanoRouteExceptionExtensions.DeveloperMessagesName], Is.EquivalentTo(new[]
                {
                    aggregate.InnerExceptions[0].ToString(),
                    aggregate.InnerExceptions[1].ToString()
                }));
            });
        }

        [Test]
        public void ConfigureExceptionHandling_ShouldReturnTheOriginalBuilder()
        {
            RouterBuilder<TestRouter, RouterConfig> result = _routerBuilder.ConfigureExceptionHandling(static config => config);

            Assert.That(result, Is.SameAs(_routerBuilder));
        }

        [Test]
        public void ExceptionHelpers_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).AddExceptionHandler())!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

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

            ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<TestRouter, RouterConfig>) null!).ConfigureExceptionHandling(static config => config))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.ConfigureExceptionHandling(null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("configure"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.ConfigureExceptionHandling(_ => null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("config"));

            ex = Assert.Throws<ArgumentNullException>(() => new ExceptionHandlingConfig { ExceptionNormalizers = null! })!;
            Assert.That(ex.ParamName, Is.EqualTo("value"));
        });

        [Test]
        public void GetErrorDetails_ShouldAcceptIntegerStatusCodes()
        {
            HttpRequestException ex = new("teapot");
            ex.Data[NanoRouteExceptionExtensions.StatusName] = 404;

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
