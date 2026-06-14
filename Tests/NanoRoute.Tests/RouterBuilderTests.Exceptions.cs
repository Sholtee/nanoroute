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
            normalized.Status = HttpStatusCode.Conflict;
            normalized.Errors = new[] { "custom-error" };
            normalized.DeveloperMessages = new[] { "custom-developer-message" };
            return normalized;
        }

        private static HttpRequestException NormalizeTeapot(Exception ex)
        {
            HttpRequestException normalized = new("teapot", ex);
            normalized.Status = (HttpStatusCode) 418;
            return normalized;
        }

        [Test]
        public async Task AddExceptionHandler_ShouldHonorConfiguredPattern()
        {
            HttpMessageRouter router = _routerBuilder
                .AddExceptionHandler("/items/")
                .AddHandler("GET", "/items/", (_, _) => throw new InvalidOperationException("boom"))
                .AddHandler("GET", "/other/", (_, _) => throw new InvalidOperationException("boom"))
                .CreateRouter();

            HttpRequestException handled = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.That(handled.Status, Is.EqualTo(HttpStatusCode.InternalServerError));

            Assert.That(async () => await router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/other"),
                s_services
            ), Throws.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public async Task AddExceptionHandler_ShouldHonorConfiguredVerbAndPattern()
        {
            HttpMessageRouter router = _routerBuilder
                .AddExceptionHandler("GET", "/items/")
                .AddHandler("GET", "/items/", (_, _) => throw new InvalidOperationException("boom"))
                .AddHandler("POST", "/items/", (_, _) => throw new InvalidOperationException("boom"))
                .CreateRouter();

            HttpRequestException handled = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.That(handled.Status, Is.EqualTo(HttpStatusCode.InternalServerError));

            Assert.That(async () => await router.Route
            (
                new HttpRequestMessage(HttpMethod.Post, "https://test.test/items"),
                s_services
            ), Throws.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void AddExceptionHandler_ShouldUseConfiguredExceptionNormalizers()
        {
            HttpMessageRouter router = _routerBuilder
                .AddExceptionHandler(options =>
                {
                    options.Map<NotSupportedException>(NormalizeConflict);
                })
                .AddHandler("GET", "/items/", (_, _) => throw new NotSupportedException("nope"))
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.Multiple(() =>
            {
                Assert.That(ex.Message, Is.EqualTo("conflict"));
                Assert.That(ex.InnerException, Is.InstanceOf<NotSupportedException>());
                Assert.That(ex.Status, Is.EqualTo(HttpStatusCode.Conflict));
                Assert.That(ex.Errors, Is.EquivalentTo(new[] { "custom-error" }));
                Assert.That(ex.DeveloperMessages, Is.EquivalentTo(new[] { "custom-developer-message" }));
            });
        }

        [Test]
        public void AddExceptionHandler_ShouldUseBaseExceptionNormalizerForDerivedExceptions()
        {
            HttpMessageRouter router = _routerBuilder
                .AddExceptionHandler(options =>
                {
                    options.Map<SystemException>(NormalizeConflict);
                })
                .AddHandler("GET", "/items/", (_, _) => throw new InvalidOperationException("nope"))
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.Multiple(() =>
            {
                Assert.That(ex.Message, Is.EqualTo("conflict"));
                Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
                Assert.That(ex.Status, Is.EqualTo(HttpStatusCode.Conflict));
            });
        }

        [Test]
        public void AddExceptionHandler_ShouldPreferExactExceptionNormalizerOverBaseNormalizer()
        {
            HttpMessageRouter router = _routerBuilder
                .AddExceptionHandler(options =>
                {
                    options.Map<SystemException>(NormalizeConflict);
                    options.Map<InvalidOperationException>(NormalizeTeapot);
                })
                .AddHandler("GET", "/items/", (_, _) => throw new InvalidOperationException("nope"))
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.Multiple(() =>
            {
                Assert.That(ex.Message, Is.EqualTo("teapot"));
                Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
                Assert.That(ex.Status, Is.EqualTo((HttpStatusCode) 418));
            });
        }

        [Test]
        public void AddExceptionHandler_ShouldIgnoreNonExceptionBaseNormalizers()
        {
            HttpMessageRouter router = _routerBuilder
                .AddExceptionHandler(options =>
                {
                    options.ExceptionNormalizers[typeof(object)] = NormalizeTeapot;
                })
                .AddHandler("GET", "/items/", (_, _) => throw new InvalidOperationException("nope"))
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.Multiple(() =>
            {
                Assert.That(ex.Message, Is.EqualTo(Properties.Resources.ERR_INTERNAL_ERROR));
                Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
                Assert.That(ex.Status, Is.EqualTo(HttpStatusCode.InternalServerError));
            });
        }

        [Test]
        public void AddExceptionHandler_ShouldSnapshotExceptionHandlingOptions()
        {
            ExceptionHandlingOptions options = new();
            options.Map<NotSupportedException>(NormalizeConflict);

            HttpMessageRouter router = _routerBuilder
                .AddExceptionHandler(["GET"], RouteScopeBuilder.CurrentPrefix, options)
                .AddHandler("GET", "/items/", (_, _) => throw new NotSupportedException("nope"))
                .CreateRouter();

            options.ExceptionNormalizers[typeof(NotSupportedException)] = NormalizeTeapot;

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.That(ex.Status, Is.EqualTo(HttpStatusCode.Conflict));
        }

        [Test]
        public void AddExceptionHandler_ShouldUseRegistrationSpecificOptions()
        {
            _routerBuilder
                .AddPrefix("/child/*", child => child
                    .AddExceptionHandler("/items/", options =>
                    {
                        options.Map<NotSupportedException>(NormalizeTeapot);
                    })
                    .AddHandler("GET", "/items/", (_, _) => throw new NotSupportedException("child")))
                .AddExceptionHandler("/parent/", options =>
                {
                    options.Map<NotSupportedException>(NormalizeConflict);
                })
                .AddHandler("GET", "/parent/", (_, _) => throw new NotSupportedException("parent"));

            HttpMessageRouter router = _routerBuilder.CreateRouter();

            HttpRequestException
                parentEx = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
                (
                    new HttpRequestMessage(HttpMethod.Get, "https://test.test/parent"),
                    s_services
                ))!,
                childEx = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
                (
                    new HttpRequestMessage(HttpMethod.Get, "https://test.test/child/items"),
                    s_services
                ))!;

            Assert.Multiple(() =>
            {
                Assert.That(parentEx.Status, Is.EqualTo(HttpStatusCode.Conflict));
                Assert.That(childEx.Status, Is.EqualTo((HttpStatusCode) 418));
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

            HttpMessageRouter router = _routerBuilder
                .AddExceptionHandler()
                .AddHandler("GET", "/items/", (_, _) => throw aggregate)
                .CreateRouter();

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route
            (
                new HttpRequestMessage(HttpMethod.Get, "https://test.test/items"),
                s_services
            ))!;

            Assert.Multiple(() =>
            {
                Assert.That(ex.Message, Is.EqualTo(Properties.Resources.ERR_INTERNAL_ERROR));
                Assert.That(ex.InnerException, Is.SameAs(aggregate));
                Assert.That(ex.Status, Is.EqualTo(HttpStatusCode.InternalServerError));
                Assert.That(ex.DeveloperMessages, Is.EquivalentTo(new[]
                {
                    aggregate.InnerExceptions[0].ToString(),
                    aggregate.InnerExceptions[1].ToString()
                }));
            });
        }

        [Test]
        public void ExceptionHelpers_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<HttpMessageRouter, RouterConfig>) null!).AddExceptionHandler())!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler((string) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler((IReadOnlyCollection<string>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler((IReadOnlyCollection<string>) null!, "/"))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler(["GET"], (string) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler((string) null!, "/"))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler("GET", (string) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => ((RouterBuilder<HttpMessageRouter, RouterConfig>) null!).AddExceptionHandler(static _ => { }))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler((Action<ExceptionHandlingOptions>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("configure"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddExceptionHandler(["GET"], "/", (ExceptionHandlingOptions) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("options"));

            ex = Assert.Throws<ArgumentNullException>(() => new ExceptionHandlingOptions { ExceptionNormalizers = null! })!;
            Assert.That(ex.ParamName, Is.EqualTo("value"));
        });

        [Test]
        public void GetErrorDetails_ShouldUseInternalServerErrorWhenStatusCodeIsMissing()
        {
            ErrorDetails details = new HttpRequestException("boom").GetErrorDetails(traceId: "trace");

            Assert.That(details.Status, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(details.Title, Is.EqualTo("boom"));
            Assert.That(details.TraceId, Is.EqualTo("trace"));
        }

        [Test]
        public void HttpRequestExceptionProperties_ShouldThrow_WhenExceptionIsNull()
        {
            HttpRequestException ex = null!;

            Assert.Multiple(() =>
            {
                ArgumentNullException thrown = Assert.Throws<ArgumentNullException>(() => _ = ex.Status)!;
                Assert.That(thrown.ParamName, Is.EqualTo("requestException"));

                thrown = Assert.Throws<ArgumentNullException>(() => ex.Status = HttpStatusCode.BadRequest)!;
                Assert.That(thrown.ParamName, Is.EqualTo("requestException"));

                thrown = Assert.Throws<ArgumentNullException>(() => _ = ex.Errors)!;
                Assert.That(thrown.ParamName, Is.EqualTo("requestException"));

                thrown = Assert.Throws<ArgumentNullException>(() => ex.Errors = new[] { "custom-error" })!;
                Assert.That(thrown.ParamName, Is.EqualTo("requestException"));

                thrown = Assert.Throws<ArgumentNullException>(() => _ = ex.DeveloperMessages)!;
                Assert.That(thrown.ParamName, Is.EqualTo("requestException"));

                thrown = Assert.Throws<ArgumentNullException>(() => ex.DeveloperMessages = new[] { "custom-developer-message" })!;
                Assert.That(thrown.ParamName, Is.EqualTo("requestException"));
            });
        }
    }
}
