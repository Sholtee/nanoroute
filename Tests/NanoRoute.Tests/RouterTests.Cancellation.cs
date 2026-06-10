/********************************************************************************
* RouterTests.Cancellation.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
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
        public void Route_ShouldBeNullChecked()
        {
            ArgumentNullException ex = Assert.ThrowsAsync<ArgumentNullException>(() => _routerBuilder.CreateRouter().Route(_request, null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("services"));
        }

        [Test]
        public void Handle_CanBeCancelled()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>((context, _) =>
                {
                    context.Cancellation.ThrowIfCancellationRequested();
                    return Task.FromResult(s_response);
                });

            InMemoryRouter router = _routerBuilder
                .AddHandler("GET", "/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/");

            using CancellationTokenSource cts = new();
            cts.Cancel();

            Assert.That(async () => await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object, cts.Token), Throws.InstanceOf<OperationCanceledException>());
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Never);
        }

        [Test]
        public void Route_ShouldPropagateCancellationDuringHandlerExecution()
        {
            using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(50));

            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request && c.Cancellation.CanBeCanceled), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (context, _) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, context.Cancellation);
                    return s_response;
                });

            InMemoryRouter router = _routerBuilder
                .AddHandler("GET", "/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/");

            Assert.That(async () => await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object, cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Route_ShouldExposeTheCallerCancellationToken()
        {
            using CancellationTokenSource cts = new();

            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request && c.Cancellation.Equals(cts.Token)), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            InMemoryRouter router = _routerBuilder
                .AddHandler("GET", "/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/");

            Assert.That(await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object, cts.Token), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public void Route_ShouldRejectUnsupportedVerbs()
        {
            InMemoryRouter router = _routerBuilder.CreateRouter();

            _request.Method = new HttpMethod("BREW");
            _request.RequestUri = new Uri("https://www.exmaple.com/");

            InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(() => router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, "BREW")));
        }
    }
}
