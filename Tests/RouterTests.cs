/********************************************************************************
* RouterTests.cs                                                                *
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
    using Internals;
    using Properties;

    [TestFixture]
    internal sealed class RouterTests
    {
        private static readonly object s_request = new();

        private static readonly HttpResponseMessage s_response = new();

        private Mock<Func<object, HttpRequestMessage>> _mockGetRequest = null!;

        private Mock<Func<HttpResponseMessage, object>> _mockGetResponse = null!;

        private DebugEventListener _debugEventListener = null!;

        private RouterBuilder<TestRouter> _routerBuilder = null!;

        private HttpRequestMessage _converted_request = null!;

        private sealed class TestRouter : Router<object, object>
        {
            protected override async Task<HttpRequestMessage> GetRequest(object request) => GetRequestImpl(request);

            protected override async Task<object> GetResponse(HttpResponseMessage response) => GetResponseImpl(response);

            public Func<object, HttpRequestMessage> GetRequestImpl { get; set; } = null!;

            public Func<HttpResponseMessage, object> GetResponseImpl { get; set; } = null!;
        }

        [OneTimeSetUp]
        public void OneTimeSetup() => _debugEventListener = new();

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _debugEventListener?.Dispose();
            _debugEventListener = null!;
        }

        [SetUp]
        public void Setup()
        {
            _converted_request = new HttpRequestMessage() { Method = HttpMethod.Get };

            _mockGetRequest = new Mock<Func<object, HttpRequestMessage>>(MockBehavior.Strict);
            _mockGetRequest
                .Setup(r => r.Invoke(s_request))
                .Returns(_converted_request);

            _mockGetResponse = new Mock<Func<HttpResponseMessage, object>>(MockBehavior.Strict);
            _mockGetResponse
                .Setup(r => r.Invoke(s_response))
                .Returns(true);

            _routerBuilder = new RouterBuilder<TestRouter>(r =>
            {
                r.GetRequestImpl = _mockGetRequest.Object;
                r.GetResponseImpl = _mockGetResponse.Object;
            });
        }

        [Test]
        public async Task Handle_ShouldMatchTheShortestPrefix()
        {
            Mock<RequestHandler>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict),
                mockHandler_3 = new(MockBehavior.Strict);

            MockSequence seq = new();

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_3
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                .AddHandler("GET", "/path/to/{some_str:any}/something/", mockHandler_3.Object) // should match 3rd
                .AddHandler("GET", "/path/to/explicit/something/", mockHandler_2.Object)  // should match 2nd
                .AddHandler("GET", "/", mockHandler_1.Object)  // should match 1st
                .AddHandler("GET", "/path/should/not/match/", new Mock<RequestHandler>(MockBehavior.Strict).Object)
                .CreateRouter();

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");

            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);

            _mockGetRequest.Verify(r => r.Invoke(s_request), Times.Once);
            _mockGetResponse.Verify(r => r.Invoke(s_response), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldMatchTheShortestPrefix_BasePrefix()
        {
            Mock<RequestHandler>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict),
                mockHandler_3 = new(MockBehavior.Strict);

            MockSequence seq = new();

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_3
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/", mockHandler_1.Object)  // should match 1st
                .AddHandler("GET", "/path/should/not/match/", new Mock<RequestHandler>(MockBehavior.Strict).Object)
                .WithBase("/path/to/", routerBuilder => routerBuilder
                    .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                    .AddHandler("GET", "/{some_str:any}/something/", mockHandler_3.Object) // should match 3rd
                    .AddHandler("GET", "/explicit/something/", mockHandler_2.Object))  // should match 2nd
                .CreateRouter();

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");

            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);

            _mockGetRequest.Verify(r => r.Invoke(s_request), Times.Once);
            _mockGetResponse.Verify(r => r.Invoke(s_response), Times.Once);
        }


        [Test]
        public async Task Handle_ShouldSupportExactMatches()
        {
            Mock<RequestHandler> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            _routerBuilder.AddHandler("GET", "/path/to/explicit/something", mockHandler.Object);

            TestRouter router = _routerBuilder.CreateRouter();

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/cica");
            HttpException ex = Assert.ThrowsAsync<HttpException>(() => router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
            Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Never);

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");
            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldSupportPrefixes()
        {
            Mock<RequestHandler> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            _routerBuilder.AddHandler("GET", "/path/to/explicit/something/", mockHandler.Object);

            TestRouter router = _routerBuilder.CreateRouter();

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/cica");
            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");
            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Exactly(2));
        }

        [Test]
        public async Task Handle_ShouldSupportMultipleHandlersAgainstTheSamePattern([Values("/path/to/explicit/something", "/path/to/{some_str:any}/something")] string pattern)
        {
            MockSequence seq = new();

            Mock<RequestHandler>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                .AddHandler("GET", pattern, mockHandler_1.Object)
                .AddHandler("GET", pattern, mockHandler_2.Object)
                .CreateRouter();

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something");
            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler_1.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
            mockHandler_2.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);

            _mockGetRequest.Verify(r => r.Invoke(s_request), Times.Once);
            _mockGetResponse.Verify(r => r.Invoke(s_response), Times.Once);
        }

        [Test]
        public async Task ExactMatch_ShouldHaveThePriority([Values] bool explicitFirst)
        {
            MockSequence seq = new();

            Mock<RequestHandler>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
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

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something");
            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler_1.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
            mockHandler_2.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task ExactMatch_ShouldHaveThePriority_BasePrefix([Values] bool explicitFirst)
        {
            MockSequence seq = new();

            Mock<RequestHandler>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (_, next) => await next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            RouterBuilder<TestRouter> pathTo = _routerBuilder
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

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something");
            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler_1.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
            mockHandler_2.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handlers_MayShareData()
        {
            MockSequence seq = new();

            Mock<RequestHandler>
                mockGetUser = new(MockBehavior.Strict),
                mockDoSomethingWithUser = new(MockBehavior.Strict);

            mockGetUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("user_id"));
                    Assert.That(cntx.Parameters["user_id"], Is.EqualTo(1986));

                    cntx.Parameters["User"] = new object();  // user object
                    return await next();
                });

            mockDoSomethingWithUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
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

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/api/users/1986/dosomething");

            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockGetUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
            mockDoSomethingWithUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handlers_MayShareData_BasePrefix()
        {
            MockSequence seq = new();

            Mock<RequestHandler>
                mockGetUser = new(MockBehavior.Strict),
                mockDoSomethingWithUser = new(MockBehavior.Strict);

            mockGetUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("user_id"));
                    Assert.That(cntx.Parameters["user_id"], Is.EqualTo(1986));

                    cntx.Parameters["User"] = new object();  // user object
                    return await next();
                });

            mockDoSomethingWithUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
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

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/api/users/1986/dosomething");

            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockGetUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
            mockDoSomethingWithUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handler_ShouldBeBoundToVerb()
        {
            Mock<RequestHandler> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder.AddHandler("POST", "path/to/somewhere", mockHandler.Object).CreateRouter();

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/path/to/somewhere");
            _converted_request.Method = HttpMethod.Get;


            HttpException ex = Assert.ThrowsAsync<HttpException>(() => router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
            Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Never);

            _converted_request.Method = HttpMethod.Post;

            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);
        }

        [Test]
        public async Task Handler_ShouldHandleMultipleVerbs()
        {
            Mock<RequestHandler> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder.AddHandler(["GET", "POST"], "path/to/somewhere", mockHandler.Object).CreateRouter();

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/path/to/somewhere");
            _converted_request.Method = HttpMethod.Get;

            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Once);

            _converted_request.Method = HttpMethod.Post;

            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Exactly(2));
        }

        [Test]
        public async Task AddHandler_ShouldCanRegisterAllVerbs()
        {
            Mock<RequestHandler> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("path/to/somewhere", mockHandler.Object)
                .CreateRouter();

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/path/to/somewhere");

            foreach (string verb in new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "TRACE" })
            {
                _converted_request.Method = new HttpMethod(verb);

                Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            }
        }

        [Test]
        public async Task Handle_ShouldBeCaseInsensitive()
        {
            Mock<RequestHandler> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "path/to/SOMEWHERE", mockHandler.Object)
                .CreateRouter();

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/PATH/to/somewhere");

            Assert.That(await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
        }

        [Test]
        public void Handle_ShouldBeNullChecked()
        {
            ArgumentNullException ex = Assert.ThrowsAsync<ArgumentNullException>(() => _routerBuilder.CreateRouter().Handle(_converted_request, null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("services"));
        }

        [Test]
        public async Task Parameters_ShouldNotLeak()
        {
            Mock<RequestHandler>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            Dictionary<string, object?>
                paramz_1 = null!,
                paramz_2 = null!;

            mockHandler_1
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
                .Returns<RequestContext, Func<Task<HttpResponseMessage>>>(async (cntx, next) =>
                {
                    paramz_1 = cntx.Parameters;
                    return await next();
                });

            mockHandler_2
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _converted_request), It.IsAny<Func<Task<HttpResponseMessage>>>()))
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
                .AddHandler("GET", "api/users/{prefix:str}/{user_id:int}/dosomething", mockHandler_1.Object)
                .AddHandler("GET", "api/users/{prefix:str}/{user_id_str:str}/dosomething", mockHandler_2.Object)
                .CreateRouter();

            _converted_request.RequestUri = new Uri("https://www.exmaple.com/api/users/whatev/1986/dosomething");

            await router.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object);

            Assert.That(paramz_1, Is.EqualTo(new Dictionary<string, object> { ["prefix"] = "whatev", ["user_id"] = 1986 }));
            Assert.That(paramz_2, Is.EqualTo(new Dictionary<string, object> { ["prefix"] = "whatev", ["user_id_str"] = "1986" }));
        }
    }
}
