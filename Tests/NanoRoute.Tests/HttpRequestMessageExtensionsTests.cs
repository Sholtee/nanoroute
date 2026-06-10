/********************************************************************************
* HttpRequestMessageExtensionsTests.cs                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net.Http;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    [TestFixture]
    internal sealed class HttpRequestMessageExtensionsTests
    {
        [Test]
        public void ContentHeaders_ShouldReturnKnownContentHeaderNames()
        {
            Assert.That(HttpRequestMessage.ContentHeaders, Is.EquivalentTo(new[]
            {
                "Allow",
                "Content-Disposition",
                "Content-Encoding",
                "Content-Language",
                "Content-Length",
                "Content-Location",
                "Content-MD5",
                "Content-Range",
                "Content-Type",
                "Expires",
                "Last-Modified"
            }));
        }

        [Test]
        public void ContentHeaders_ShouldIgnoreHeaderNameCasing()
        {
            Assert.That(HttpRequestMessage.ContentHeaders.Contains("content-type"), Is.True);
            Assert.That(HttpRequestMessage.ContentHeaders.Contains("LAST-MODIFIED"), Is.True);
        }

        [Test]
        public void OriginalRequest_ShouldReturnNullByDefault()
        {
            using HttpRequestMessage request = new();

            Assert.That(request.OriginalRequest, Is.Null);
        }

        [Test]
        public void OriginalRequest_ShouldStoreAndRemoveValue()
        {
            using HttpRequestMessage request = new();
            object originalRequest = new();

            request.OriginalRequest = originalRequest;

            Assert.That(request.OriginalRequest, Is.SameAs(originalRequest));
            Assert.That(request.TryGetProperty("OriginalRequest", out object? stored), Is.True);
            Assert.That(stored, Is.SameAs(originalRequest));

            request.OriginalRequest = null;

            Assert.That(request.OriginalRequest, Is.Null);
            Assert.That(request.TryGetProperty("OriginalRequest", out _), Is.False);
        }

        [Test]
        public void TraceId_ShouldReturnNullByDefault()
        {
            using HttpRequestMessage request = new();

            Assert.That(request.TraceId, Is.Null);
        }

        [Test]
        public void TraceId_ShouldStoreAndRemoveValue()
        {
            using HttpRequestMessage request = new();

            request.TraceId = "trace-1";

            Assert.That(request.TraceId, Is.EqualTo("trace-1"));
            Assert.That(request.TryGetProperty("TraceId", out object? stored), Is.True);
            Assert.That(stored, Is.EqualTo("trace-1"));

            request.TraceId = null;

            Assert.That(request.TraceId, Is.Null);
            Assert.That(request.TryGetProperty("TraceId", out _), Is.False);
        }

        [Test]
        public void TraceId_ShouldStringify_WhenStoredValueIsNotAString()
        {
            using HttpRequestMessage request = new();

            request.SetProperty("TraceId", 1986);

            Assert.That(request.TraceId, Is.EqualTo("1986"));
        }

        [Test]
        public void InstanceExtensions_ShouldThrow_WhenRequestIsNull()
        {
            HttpRequestMessage request = null!;

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _ = request.OriginalRequest)!;
            Assert.That(ex.ParamName, Is.EqualTo("request"));

            ex = Assert.Throws<ArgumentNullException>(() => request.OriginalRequest = new object())!;
            Assert.That(ex.ParamName, Is.EqualTo("request"));

            ex = Assert.Throws<ArgumentNullException>(() => _ = request.TraceId)!;
            Assert.That(ex.ParamName, Is.EqualTo("request"));

            ex = Assert.Throws<ArgumentNullException>(() => request.TraceId = "trace-1")!;
            Assert.That(ex.ParamName, Is.EqualTo("request"));
        }
    }
}
