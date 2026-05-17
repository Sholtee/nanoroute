/********************************************************************************
* RouterTests.Handlers.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Properties;

    internal sealed partial class RouterTests
    {
        [Test]
        public async Task Handlers_MayShareData()
        {
            MockSequence seq = new();

            Mock<RequestHandlerDelegate>
                mockGetUser = new(MockBehavior.Strict),
                mockDoSomethingWithUser = new(MockBehavior.Strict);

            object userObj = new();

            mockGetUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("user_id").WithValue(1986));

                    cntx.Parameters["User"] = userObj;
                    return await next();
                });

            mockDoSomethingWithUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("User").WithValue(userObj));

                    return s_response;
                });

            TestRouter router = _routerBuilder
                .AddValueParser("int", (ReadOnlyMemory<char> segment, object? _, out object? parsed) =>
                {
                    if (int.TryParse(segment.ToString(), out int userId))
                    {
                        parsed = userId;
                        return true;
                    }
                    parsed = null;
                    return false;
                })
                .AddHandler("GET", "/api/users/{user_id:int}/*", mockGetUser.Object)
                .AddHandler("GET", "/api/users/{user_id:int}/dosomething/", mockDoSomethingWithUser.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/api/users/1986/dosomething");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockGetUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
            mockDoSomethingWithUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handlers_MayShareData_BasePrefix()
        {
            MockSequence seq = new();

            Mock<RequestHandlerDelegate>
                mockGetUser = new(MockBehavior.Strict),
                mockDoSomethingWithUser = new(MockBehavior.Strict);

            object userObj = new();

            mockGetUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("user_id").WithValue(1986));

                    cntx.Parameters["User"] = userObj;
                    return await next();
                });

            mockDoSomethingWithUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("User").WithValue(userObj));

                    return s_response;
                });

            TestRouter router = _routerBuilder
                .AddValueParser("int", (ReadOnlyMemory<char> segment, object? _, out object? parsed) =>
                {
                    if (int.TryParse(segment.ToString(), out int userId))
                    {
                        parsed = userId;
                        return true;
                    }
                    parsed = null;
                    return false;
                })
                .AddPrefix("/api/users/{user_id:int}/*", routerBuilder => routerBuilder
                    .AddHandler("GET", RouteScopeBuilder.CurrentPrefix, mockGetUser.Object)
                    .AddHandler("GET", "/dosomething/", mockDoSomethingWithUser.Object))
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/api/users/1986/dosomething");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockGetUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
            mockDoSomethingWithUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handler_ShouldBeBoundToVerb()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder.AddHandler("POST", "/path/to/somewhere/", mockHandler.Object).CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/somewhere");
            _request.Method = HttpMethod.Get;

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
            Assert.That(ex.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Never);

            _request.Method = HttpMethod.Post;

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handler_ShouldHandleMultipleVerbs()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler(["GET", "POST"], "/path/to/somewhere/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/somewhere");
            _request.Method = HttpMethod.Get;

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);

            _request.Method = HttpMethod.Post;

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Exactly(2));
        }

        [Test]
        public async Task Handle_ShouldPropagateTheServiceProvider()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            Mock<IServiceProvider> mockServices = new(MockBehavior.Strict);

            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request && c.Services == mockServices.Object), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/");

            Assert.That(await router.Handle(_request, mockServices.Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }
#if !NETFRAMEWORK
        [Test]
        public async Task Handle_ShouldRespectConfiguredParametersCapacity()
        {
            const int parametersCapacity = 32;

            int capturedCapacity = 0;

            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .ConfigureRouting(config => config with { ParametersCapacity = parametersCapacity })
                .AddHandler("GET", "/users/{user_id:int}/items/{item_id:int}/", async (context, _) =>
                {
                    capturedCapacity = context.Parameters.EnsureCapacity(0);
                    return s_response;
                })
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/users/1986/items/42");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            Assert.That(capturedCapacity, Is.GreaterThanOrEqualTo(parametersCapacity));
        }
#endif
    }
}
