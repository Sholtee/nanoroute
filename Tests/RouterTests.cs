/********************************************************************************
* RouterTests.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;
    using Properties;

    [TestFixture]
    internal sealed class RouterTests
    {
        private static readonly HttpResponseMessage s_response = new();

        private DebugEventListener _debugEventListener = null!;

        private RouterBuilder<TestRouter, RouterConfig> _routerBuilder = null!;

        private HttpRequestMessage _request = null!;

        private sealed class TestRouter(RouterBuilder<TestRouter, RouterConfig> builder) : Router(builder, builder.RouterConfig)
        {
        }

        [SetUp]
        public void Setup()
        {
            _request = new HttpRequestMessage() { Method = HttpMethod.Get };
            _debugEventListener = new DebugEventListener(EventLevel.LogAlways);
            _routerBuilder = new RouterBuilder<TestRouter, RouterConfig>(bldr => new TestRouter(bldr));
        }

        [TearDown]
        public void TearDown()
        {
            _debugEventListener?.Dispose();
        }

        [Test]
        public async Task Handle_ShouldMatchTheShortestPrefix()
        {
            Mock<RequestHandlerDelegate>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict),
                mockHandler_3 = new(MockBehavior.Strict);

            MockSequence seq = new();

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_3
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                .AddHandler("GET", "/path/to/{some_str:any}/something/", mockHandler_3.Object) // should match 3rd
                .AddHandler("GET", "/path/to/explicit/something/", mockHandler_2.Object)  // should match 2nd
                .AddHandler("GET", "/", mockHandler_1.Object)  // should match 1st
                .AddHandler("GET", "/path/should/not/match/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
        }

        [Test]
        public async Task Handle_ShouldMatchTheShortestPrefix_BasePrefix()
        {
            Mock<RequestHandlerDelegate>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict),
                mockHandler_3 = new(MockBehavior.Strict);

            MockSequence seq = new();

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_3
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/", mockHandler_1.Object)  // should match 1st
                .AddHandler("GET", "/path/should/not/match/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .WithBase("/path/to/", routerBuilder => routerBuilder
                    .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                    .AddHandler("GET", "/{some_str:any}/something/", mockHandler_3.Object) // should match 3rd
                    .AddHandler("GET", "/explicit/something/", mockHandler_2.Object))  // should match 2nd
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
        }

        [Test]
        public async Task Handle_ShouldWorkWithEmptyPaths([Values("", "/")] string path)
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", path, mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
        }

        [Test]
        public async Task Handle_ShouldSupportExactMatches()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            _routerBuilder.AddHandler("GET", "/path/to/explicit/something", mockHandler.Object);

            TestRouter router = _routerBuilder.CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/cica");
            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
            Assert.That(ex.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Never);

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldSupportPrefixes()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            _routerBuilder.AddHandler("GET", "/path/to/explicit/something/", mockHandler.Object);

            TestRouter router = _routerBuilder.CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/cica");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Exactly(2));
        }

        [Test]
        public async Task Handle_ShouldSupportMultipleHandlersAgainstTheSamePattern([Values("/path/to/explicit/something", "/path/to/{some_str:any}/something")] string pattern)
        {
            MockSequence seq = new();

            Mock<RequestHandlerDelegate>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                .AddHandler("GET", pattern, mockHandler_1.Object)
                .AddHandler("GET", pattern, mockHandler_2.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler_1.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
            mockHandler_2.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task ExactMatch_ShouldHaveThePriority([Values] bool explicitFirst)
        {
            MockSequence seq = new();

            Mock<RequestHandlerDelegate>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            _routerBuilder.AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; });

            if (explicitFirst)
                _routerBuilder
                    .AddHandler("GET", "/path/to/explicit/something", mockHandler_1.Object)
                    .AddHandler("GET", "/path/to/{some_str:any}/something", mockHandler_2.Object);
            else
                _routerBuilder
                    .AddHandler("GET", "/path/to/{some_str:any}/something", mockHandler_2.Object)
                    .AddHandler("GET", "/path/to/explicit/something", mockHandler_1.Object);

            TestRouter router = _routerBuilder.CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler_1.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
            mockHandler_2.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task ExactMatch_ShouldHaveThePriority_BasePrefix([Values] bool explicitFirst)
        {
            MockSequence seq = new();

            Mock<RequestHandlerDelegate>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            RouteBuilder pathTo = _routerBuilder
                .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                .WithBase("/path/to/");

            if (explicitFirst)
                pathTo
                    .AddHandler("GET", "explicit/something", mockHandler_1.Object)
                    .AddHandler("GET", "{some_str:any}/something", mockHandler_2.Object);
            else
                pathTo
                    .AddHandler("GET", "{some_str:any}/something", mockHandler_2.Object)
                    .AddHandler("GET", "explicit/something", mockHandler_1.Object);

            TestRouter router = _routerBuilder.CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler_1.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
            mockHandler_2.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldRespectConfiguredMatchingBehavior([Values(MatchingBehavior.LiteralFirst, MatchingBehavior.ParameterizedChildrenFirst)] MatchingBehavior matchingBehavior)
        {
            Mock<RequestHandlerDelegate>
                mockLiteralHandler = new(MockBehavior.Strict),
                mockParameterizedHandler = new(MockBehavior.Strict);

            mockLiteralHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            mockParameterizedHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["value"], "literal")),
                    It.IsAny<Func<Task<HttpResponseMessage>>>()
                ))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddParameterParser("str", (string segment, out object? parsed) =>
                {
                    parsed = segment;
                    return true;
                })
                .WithConfiguration(config => config.MatchingBehavior = matchingBehavior)
                .AddHandler("GET", "/items/literal", mockLiteralHandler.Object)
                .AddHandler("GET", "/items/{value:str}", mockParameterizedHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/items/literal");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockLiteralHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), matchingBehavior == MatchingBehavior.LiteralFirst ? Times.Once() : Times.Never());
            mockParameterizedHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), matchingBehavior == MatchingBehavior.ParameterizedChildrenFirst ? Times.Once() : Times.Never());
        }

        [Test]
        public void Handle_ShouldRejectUnknownMatchingBehavior()
        {
            _routerBuilder.WithConfiguration(config => config.MatchingBehavior = (MatchingBehavior) 100);

            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => _routerBuilder.CreateRouter())!;
            Assert.That(ex.ParamName, Is.EqualTo("value"));
        }

        [Test]
        public async Task Handlers_MayShareData()
        {
            MockSequence seq = new();

            Mock<RequestHandlerDelegate>
                mockGetUser = new(MockBehavior.Strict),
                mockDoSomethingWithUser = new(MockBehavior.Strict);

            mockGetUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("user_id"));
                    Assert.That(cntx.Parameters["user_id"], Is.EqualTo(1986));

                    cntx.Parameters["User"] = new object();  // user object
                    return await next();
                });

            mockDoSomethingWithUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("User"));
                    Assert.That(cntx.Parameters["User"], Is.InstanceOf<object>());

                    return s_response;
                });

            TestRouter router = _routerBuilder
                .AddParameterParser("int", (string segment, out object? parsed) =>
                {
                    if (int.TryParse(segment, out int userId))
                    {
                        parsed = userId;
                        return true;
                    }
                    parsed = null;
                    return false;
                })
                .AddHandler("GET", "api/users/{user_id:int}/", mockGetUser.Object)
                .AddHandler("GET", "api/users/{user_id:int}/dosomething", mockDoSomethingWithUser.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/api/users/1986/dosomething");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockGetUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
            mockDoSomethingWithUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handlers_MayShareData_BasePrefix()
        {
            MockSequence seq = new();

            Mock<RequestHandlerDelegate>
                mockGetUser = new(MockBehavior.Strict),
                mockDoSomethingWithUser = new(MockBehavior.Strict);

            mockGetUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("user_id"));
                    Assert.That(cntx.Parameters["user_id"], Is.EqualTo(1986));

                    cntx.Parameters["User"] = new object();  // user object
                    return await next();
                });

            mockDoSomethingWithUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("User"));
                    Assert.That(cntx.Parameters["User"], Is.InstanceOf<object>());

                    return s_response;
                });

            TestRouter router = _routerBuilder
                .AddParameterParser("int", (string segment, out object? parsed) =>
                {
                    if (int.TryParse(segment, out int userId))
                    {
                        parsed = userId;
                        return true;
                    }
                    parsed = null;
                    return false;
                })
                .WithBase("api/users/{user_id:int}/", routerBuilder => routerBuilder
                    .AddHandler("GET", "/", mockGetUser.Object)
                    .AddHandler("GET", "/dosomething", mockDoSomethingWithUser.Object))
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/api/users/1986/dosomething");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockGetUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
            mockDoSomethingWithUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldSupportParametersWithoutName()
        {
            Dictionary<string, object?> paramz = null!;

            TestRouter router = _routerBuilder
                .AddDefaultParsers()
                .AddHandler("GET", "users/{user_id:int}/{str}/cica", async (context, next) =>
                {
                    paramz = context.Parameters;
                    return s_response;
                })
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/users/1986/any_string/cica");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            Assert.That(paramz, Has.Count.EqualTo(1));
            Assert.That(paramz["user_id"], Is.EqualTo(1986));
        }

        [Test]
        public async Task Handler_ShouldBeBoundToVerb()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder.AddHandler("POST", "path/to/somewhere", mockHandler.Object).CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/somewhere");
            _request.Method = HttpMethod.Get;


            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
            Assert.That(ex.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Never);

            _request.Method = HttpMethod.Post;

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handler_ShouldHandleMultipleVerbs()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder.AddHandler(["GET", "POST"], "path/to/somewhere", mockHandler.Object).CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/somewhere");
            _request.Method = HttpMethod.Get;

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);

            _request.Method = HttpMethod.Post;

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Exactly(2));
        }

        [Test]
        public async Task AddHandler_ShouldCanRegisterAllVerbs()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("path/to/somewhere", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/somewhere");

            foreach (string verb in new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "TRACE" })
            {
                _request.Method = new HttpMethod(verb);

                Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            }
        }

        [Test]
        public async Task Handle_ShouldBeCaseInsensitive()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "path/to/SOMEWHERE", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/PATH/to/somewhere");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
        }

        [Test]
        public void Handle_ShouldBeNullChecked()
        {
            ArgumentNullException ex = Assert.ThrowsAsync<ArgumentNullException>(() => _routerBuilder.CreateRouter().Handle(_request, null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("services"));
        }

        [Test]
        public void Handle_CanBeCancelled()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>((context, _) =>
                {
                    context.Cancellation.ThrowIfCancellationRequested();
                    return Task.FromResult(s_response);
                });

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/");

            using CancellationTokenSource cts = new();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(() => router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object, cts.Token));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldPropagateTheServiceProvider()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            Mock<IServiceProvider> mockServices = new(MockBehavior.Strict);

            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request && c.Services == mockServices.Object), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/");

            Assert.That(await router.Handle(_request, mockServices.Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldNotLeakTheParameters_1()
        {
            Mock<RequestHandlerDelegate>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            Dictionary<string, object?>
                paramz_1 = null!,
                paramz_2 = null!;

            mockHandler_1
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (cntx, next) =>
                {
                    paramz_1 = cntx.Parameters;
                    return await next();
                });

            mockHandler_2
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (cntx, next) =>
                {
                    paramz_2 = cntx.Parameters;
                    return s_response;
                });

            TestRouter router = _routerBuilder
                .AddParameterParser("int", (string segment, out object? parsed) =>
                {
                    if (int.TryParse(segment, out int userId))
                    {
                        parsed = userId;
                        return true;
                    }
                    parsed = null;
                    return false;
                })
                .AddParameterParser("str", (string segment, out object? parsed) =>
                {
                    parsed = segment;
                    return true;
                })
                .WithBase("/api/users/", bldr => bldr
                    .AddHandler("GET", "/{prefix:str}/{user_id:int}/dosomething", mockHandler_1.Object)
                    .AddHandler("GET", "/{prefix:str}/{user_id_str:str}/dosomething", mockHandler_2.Object))
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/api/users/whatev/1986/dosomething");

            await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object);

            Assert.That(paramz_1, Is.EqualTo(new Dictionary<string, object> { ["prefix"] = "whatev", ["user_id"] = 1986 }));
            Assert.That(paramz_2, Is.EqualTo(new Dictionary<string, object> { ["prefix"] = "whatev", ["user_id_str"] = "1986" }));
        }

        [Test]
        public async Task Handle_ShouldNotLeakTheParameters_2()
        {
            Mock<RequestHandlerDelegate>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            Dictionary<string, object?>
                paramz_1 = null!,
                paramz_2 = null!;

            mockHandler_1
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (cntx, next) =>
                {
                    paramz_1 = cntx.Parameters;
                    return await next();
                });

            mockHandler_2
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (cntx, next) =>
                {
                    paramz_2 = cntx.Parameters;
                    return s_response;
                });

            TestRouter router = _routerBuilder
                .AddParameterParser("int", (string segment, out object? parsed) =>
                {
                    if (int.TryParse(segment, out int userId))
                    {
                        parsed = userId;
                        return true;
                    }
                    parsed = null;
                    return false;
                })
                .WithConfiguration(config => config.MatchingBehavior = MatchingBehavior.ParameterizedChildrenFirst)
                .WithBase("/api/users/", bldr => bldr
                    .AddHandler("GET", "/{user_id:int}/dosomething", mockHandler_1.Object)
                    .AddHandler("GET", "/1986/dosomething", mockHandler_2.Object))
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/api/users/1986/dosomething");

            await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object);

            Assert.That(paramz_1, Is.EqualTo(new Dictionary<string, object> { ["user_id"] = 1986 }));
            Assert.That(paramz_2, Is.EqualTo(new Dictionary<string, object>(0)));
        }

        [Test]
        public void Handle_ShouldRejectUnsupportedVerbs()
        {
            TestRouter router = _routerBuilder.CreateRouter();

            _request.Method = new HttpMethod("BREW");
            _request.RequestUri = new Uri("https://www.exmaple.com/");

            ArgumentException ex = Assert.ThrowsAsync<ArgumentException>(() => router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("request"));
            Assert.That(ex.Message, Does.Contain(string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, "BREW")));
        }

        [Test]
        public async Task Handle_ShouldNormalizeEscapedPaths()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/users/~denes", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/users/%7Edenes");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldMatchPercentEncodedLiteralSegments()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/files/a%20b", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/files/a%20b");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldPassDecodedSegmentsToParameterParsers()
        {
            Mock<ParameterParserDelegate> mockParser = new(MockBehavior.Strict);
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);

            object? parsed = null;
            mockParser
                .Setup(p => p.Invoke("a b", out parsed))
                .Returns((string segment, out object? parsed) =>
                {
                    parsed = segment;
                    return true;
                });

            mockHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["name"], "a b")),
                    It.IsAny<Func<Task<HttpResponseMessage>>>()
                ))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddParameterParser("str", mockParser.Object)
                .AddHandler("GET", "/files/{name:str}", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/files/a%20b");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockParser.Verify(p => p.Invoke("a b", out parsed), Times.Once);
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldNormalizeDotSegmentsInUriPaths()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/users/denes", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/users/../users/denes");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldPreferLiteralSegmentsOverParameterizedSegmentsInDeeperBranches()
        {
            Mock<RequestHandlerDelegate>
                mockLiteralHandler = new(MockBehavior.Strict),
                mockParameterizedHandler = new(MockBehavior.Strict);

            mockLiteralHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                .WithBase("/api/", bldr => bldr
                    .AddHandler("GET", "/{scope:any}/details/settings", mockLiteralHandler.Object)
                    .AddHandler("GET", "/{scope:any}/details/{section:any}", mockParameterizedHandler.Object))
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/api/admin/details/settings");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockLiteralHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
            mockParameterizedHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Never);
        }

        [Test]
        public async Task Handle_ShouldContinueMatchingWhenAParameterParserReturnsFalse()
        {
            Mock<RequestHandlerDelegate>
                mockIntHandler = new(MockBehavior.Strict),
                mockStringHandler = new(MockBehavior.Strict);

            Mock<ParameterParserDelegate>
                mockIntParser = new(MockBehavior.Strict),
                mockStringParser = new(MockBehavior.Strict);

            MockSequence seq = new();

            object? parsed = null;
            mockIntParser
                .InSequence(seq)
                .Setup(p => p.Invoke("abc", out parsed))
                .Returns(false);

            mockStringParser
                .InSequence(seq)
                .Setup(p => p.Invoke("abc", out parsed))
                .Returns((string segment, out object? parsed) =>
                {
                    parsed = segment;
                    return true;
                });

            mockStringHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["slug"], "abc")),
                    It.IsAny<Func<Task<HttpResponseMessage>>>()
                ))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddParameterParser("int", mockIntParser.Object)
                .AddParameterParser("str", mockStringParser.Object)
                .WithBase("/api/", bldr => bldr
                    .AddHandler("GET", "/{id:int}/details", mockIntHandler.Object)
                    .AddHandler("GET", "/{slug:str}/details", mockStringHandler.Object))
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/api/abc/details");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockIntHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Never);
            mockStringHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
            mockIntParser.Verify(p => p.Invoke("abc", out parsed), Times.Once);
            mockStringParser.Verify(p => p.Invoke("abc", out parsed), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldLogTheRequestLifecycle()
        {
            TestRouter router = _routerBuilder
                .AddHandler("GET", "/path/to/somewhere", async (_, _) => s_response)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/somewhere");

            HttpResponseMessage response = await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object);

            Assert.That(response, Is.EqualTo(s_response));
            Assert.That(SpinWait.SpinUntil(() => _debugEventListener.Events.Count >= 2, 1000), Is.True);

            EventWrittenEventArgs
                requestStarted = _debugEventListener.Events.Single(e => e.EventName == "RequestProcessingStarted"),
                matchingHandler = _debugEventListener.Events.Single(e => e.EventName == "MatchingHandler");

            Assert.Multiple(() =>
            {
                Assert.That(requestStarted.Level, Is.EqualTo(EventLevel.Informational));
                Assert.That(requestStarted.PayloadNames, Is.EquivalentTo(new[] { "RequestPath", "Verb" }));
                Assert.That(requestStarted.Payload, Is.EquivalentTo(new object?[] { "/path/to/somewhere", HttpVerb.Get }));

                Assert.That(matchingHandler.Level, Is.EqualTo(EventLevel.Informational));
                Assert.That(matchingHandler.PayloadNames, Is.EquivalentTo(new[] { "RequestPath", "Verb", "Pattern", "ParameterCount" }));
                Assert.That(matchingHandler.Payload, Is.EquivalentTo(new object?[] { "/path/to/somewhere", HttpVerb.Get, "/path/to/somewhere", 0 }));
            });
        }

        [Test]
        public void Handle_ShouldLogWhenNoHandlerMatches()
        {
            TestRouter router = _routerBuilder.CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/nowhere");

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
            Assert.That(ex.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(SpinWait.SpinUntil(() => _debugEventListener.Events.Count >= 2, 1000), Is.True);

            EventWrittenEventArgs
                requestStarted = _debugEventListener.Events.Single(e => e.EventName == "RequestProcessingStarted"),
                noMatchingHandler = _debugEventListener.Events.Single(e => e.EventName == "NoMatchingHandler");

            Assert.Multiple(() =>
            {
                Assert.That(requestStarted.Level, Is.EqualTo(EventLevel.Informational));
                Assert.That(requestStarted.PayloadNames, Is.EquivalentTo(new[] { "RequestPath", "Verb" }));
                Assert.That(requestStarted.Payload, Is.EquivalentTo(new object?[] { "/path/to/nowhere", HttpVerb.Get }));

                Assert.That(noMatchingHandler.Level, Is.EqualTo(EventLevel.Informational));
                Assert.That(noMatchingHandler.PayloadNames, Is.EquivalentTo(new[] { "RequestPath", "Verb" }));
                Assert.That(noMatchingHandler.Payload, Is.EquivalentTo(new object?[] { "/path/to/nowhere", HttpVerb.Get }));
            });
        }
    }
}
