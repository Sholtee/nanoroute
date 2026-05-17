/********************************************************************************
* RouterTests.PathNormalization.cs                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    internal sealed partial class RouterTests
    {
        [Test]
        public async Task Handle_ShouldBeCaseInsensitive()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/path/to/SOMEWHERE/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/PATH/to/somewhere");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
        }

        [Test]
        public async Task Handle_ShouldNormalizeEscapedPaths()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/users/~denes/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/users/%7Edenes");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldMatchPercentEncodedLiteralSegments()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/files/a%20b/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/files/a%20b");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldPreservePlusInPathSegments()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/files/a+b/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/files/a+b");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldNormalizeDotSegmentsInUriPaths()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/users/denes/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/users/../users/denes");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }
    }
}
