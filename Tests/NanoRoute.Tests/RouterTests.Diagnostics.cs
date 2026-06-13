/********************************************************************************
* RouterTests.Diagnostics.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Properties;

    internal sealed partial class RouterTests
    {
        [Test]
        public async Task Route_ShouldLogTheRequestLifecycle()
        {
            HttpMessageRouter router = _routerBuilder
                .AddHandler("GET", "/path/to/somewhere/", async (_, _) => s_response)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/somewhere");

            HttpResponseMessage response = await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object);

            Assert.That(response, Is.EqualTo(s_response));
            Assert.That(SpinWait.SpinUntil(() => _debugEventListener.Events.Count >= 2, 1000), Is.True);

            EventWrittenEventArgs
                requestStarted = _debugEventListener.Events.Single(e => e.EventName == "RequestProcessingStarted"),
                matchingHandler = _debugEventListener.Events.Single(e => e.EventName == "MatchingHandler");

            Assert.Multiple(() =>
            {
                Assert.That(requestStarted.Level, Is.EqualTo(EventLevel.Informational));
                Assert.That(requestStarted.PayloadNames, Is.EquivalentTo(new[] { "RequestUri", "Verb" }));
                Assert.That(requestStarted.Payload, Is.EquivalentTo(new object?[] { "https://www.exmaple.com/path/to/somewhere", HttpMethod.Get.Method }));

                Assert.That(matchingHandler.Level, Is.EqualTo(EventLevel.Informational));
                Assert.That(matchingHandler.PayloadNames, Is.EquivalentTo(new[] { "RequestUri", "Verb", "Pattern", "ParameterCount" }));
                Assert.That(matchingHandler.Payload, Is.EquivalentTo(new object?[] { "https://www.exmaple.com/path/to/somewhere", HttpMethod.Get.Method, "/path/to/somewhere/", 0 }));
            });
        }

        [Test]
        public void Route_ShouldLogWhenNoHandlerMatches()
        {
            HttpMessageRouter router = _routerBuilder.CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/nowhere");

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
            Assert.That(ex.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(SpinWait.SpinUntil(() => _debugEventListener.Events.Count >= 2, 1000), Is.True);

            EventWrittenEventArgs
                requestStarted = _debugEventListener.Events.Single(e => e.EventName == "RequestProcessingStarted"),
                noMatchingHandler = _debugEventListener.Events.Single(e => e.EventName == "NoMatchingHandler");

            Assert.Multiple(() =>
            {
                Assert.That(requestStarted.Level, Is.EqualTo(EventLevel.Informational));
                Assert.That(requestStarted.PayloadNames, Is.EquivalentTo(new[] { "RequestUri", "Verb" }));
                Assert.That(requestStarted.Payload, Is.EquivalentTo(new object?[] { "https://www.exmaple.com/path/to/nowhere", HttpMethod.Get.Method }));

                Assert.That(noMatchingHandler.Level, Is.EqualTo(EventLevel.Informational));
                Assert.That(noMatchingHandler.PayloadNames, Is.EquivalentTo(new[] { "RequestUri", "Verb" }));
                Assert.That(noMatchingHandler.Payload, Is.EquivalentTo(new object?[] { "https://www.exmaple.com/path/to/nowhere", HttpMethod.Get.Method }));
            });
        }
    }
}
