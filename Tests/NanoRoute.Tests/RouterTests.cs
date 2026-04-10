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
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (_, next) => await next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (_, next) => await next());

            mockHandler_3
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddSegmentParser("any", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = segment.ToString(); return true; })
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
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (_, next) => await next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (_, next) => await next());

            mockHandler_3
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/", mockHandler_1.Object)  // should match 1st
                .AddHandler("GET", "/path/should/not/match/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .WithBase("/path/to/", routerBuilder => routerBuilder
                    .AddSegmentParser("any", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = segment.ToString(); return true; })
                    .AddHandler("GET", "/{some_str:any}/something/", mockHandler_3.Object) // should match 3rd
                    .AddHandler("GET", "/explicit/something/", mockHandler_2.Object))  // should match 2nd
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
        }

        [TestCase("")]
        [TestCase("/")]
        public async Task Handle_ShouldWorkWithEmptyPaths(string path)
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
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
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            _routerBuilder.AddHandler("GET", "/path/to/explicit/something", mockHandler.Object);

            TestRouter router = _routerBuilder.CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/cica");
            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
            Assert.That(ex.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Never);

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldSupportPrefixes()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            _routerBuilder.AddHandler("GET", "/path/to/explicit/something/", mockHandler.Object);

            TestRouter router = _routerBuilder.CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/cica");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Exactly(2));
        }

        [TestCase("/path/to/explicit/something")]
        [TestCase("/path/to/{some_str:any}/something")]
        public async Task Handle_ShouldSupportMultipleHandlersAgainstTheSamePattern(string pattern)
        {
            MockSequence seq = new();

            Mock<RequestHandlerDelegate>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (_, next) => await next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddSegmentParser("any", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = segment.ToString(); return true; })
                .AddHandler("GET", pattern, mockHandler_1.Object)
                .AddHandler("GET", pattern, mockHandler_2.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler_1.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
            mockHandler_2.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [TestCase(MatchingBehavior.LiteralFirst)]
        [TestCase(MatchingBehavior.ParameterizedChildrenFirst)]
        public async Task Handle_ShouldRespectConfiguredMatchingBehavior(MatchingBehavior matchingBehavior)
        {
            Mock<RequestHandlerDelegate>
                mockLiteralHandler = new(MockBehavior.Strict),
                mockParameterizedHandler = new(MockBehavior.Strict);

            mockLiteralHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            mockParameterizedHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["value"], "literal")),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddSegmentParser("str", (ReadOnlyMemory<char> segment, object? _, out object? parsed) =>
                {
                    parsed = segment.ToString();
                    return true;
                })
                .WithConfiguration(config => config.MatchingBehavior = matchingBehavior)
                .AddHandler("GET", "/items/literal", mockLiteralHandler.Object)
                .AddHandler("GET", "/items/{value:str}", mockParameterizedHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/items/literal");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockLiteralHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), matchingBehavior == MatchingBehavior.LiteralFirst ? Times.Once() : Times.Never());
            mockParameterizedHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), matchingBehavior == MatchingBehavior.ParameterizedChildrenFirst ? Times.Once() : Times.Never());
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
                .AddSegmentParser("int", (ReadOnlyMemory<char> segment, object? _, out object? parsed) =>
                {
                    if (int.TryParse(segment.ToString(), out int userId))
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
                .AddSegmentParser("int", (ReadOnlyMemory<char> segment, object? _, out object? parsed) =>
                {
                    if (int.TryParse(segment.ToString(), out int userId))
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
            mockGetUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
            mockDoSomethingWithUser.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldSupportParsedSegmentsWithoutBindingTheirValueToParameters()
        {
            Dictionary<string, object?> paramz = null!;
            int parserCalls = 0;

            bool ParseSlug(ReadOnlyMemory<char> segment, object? _, out object? parsed)
            {
                parserCalls++;
                Assert.That(segment.ToString(), Is.EqualTo("any_string"));
                parsed = segment.ToString();
                return true;
            }

            TestRouter router = _routerBuilder
                .AddDefaultParsers()
                .AddSegmentParser("slug", (SyncSegmentParserDelegate) ParseSlug)
                .AddHandler("GET", "users/{user_id:int}/{slug}/cica", async (context, next) =>
                {
                    paramz = context.Parameters;
                    return s_response;
                })
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/users/1986/any_string/cica");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            Assert.That(parserCalls, Is.EqualTo(1));
            Assert.That(paramz, Has.Count.EqualTo(1));
            Assert.That(paramz, Does.ContainKey("user_id").WithValue(1986));
            Assert.That(paramz, Does.Not.ContainKey("slug"));
        }

        [Test]
        public async Task Handler_ShouldBeBoundToVerb()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder.AddHandler("POST", "path/to/somewhere", mockHandler.Object).CreateRouter();

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

            TestRouter router = _routerBuilder.AddHandler(["GET", "POST"], "path/to/somewhere", mockHandler.Object).CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/somewhere");
            _request.Method = HttpMethod.Get;

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);

            _request.Method = HttpMethod.Post;

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Exactly(2));
        }

        [Test]
        public async Task AddHandler_ShouldCanRegisterAllVerbs()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
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
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
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
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>((context, _) =>
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

            Assert.That(async () => await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object, cts.Token), Throws.InstanceOf<OperationCanceledException>());
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Never);
        }

        [Test]
        public void Handle_ShouldCancelWhenTimeoutExpires()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request && c.Cancellation.CanBeCanceled), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (context, _) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, context.Cancellation);
                    return s_response;
                });

            TestRouter router = _routerBuilder
                .WithConfiguration(config => config.Timeout = TimeSpan.FromMilliseconds(50))
                .AddHandler("GET", "/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/");

            Assert.That(async () => await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Throws.InstanceOf<OperationCanceledException>());
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
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

        [Test]
        public async Task Handle_ShouldStayOnTheSelectedParameterizedBranch()
        {
            Mock<RequestHandlerDelegate>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            Mock<SegmentParserDelegate>
                mockIntParser = new(MockBehavior.Strict),
                mockStringParser = new(MockBehavior.Strict);

            Dictionary<string, object?>
                paramz_1 = null!,
                paramz_2 = null!;

            mockIntParser
                .Setup(parser => parser.Invoke(It.Is<SegmentParserContext>(context => context.Segment.ToString() == "1986")))
                .Returns(new ValueTask<SegmentParseResult>(new SegmentParseResult(true, 1986)));

            mockStringParser
                .Setup(parser => parser.Invoke(It.Is<SegmentParserContext>(context => context.Segment.ToString() == "whatev")))
                .Returns((SegmentParserContext context) => new ValueTask<SegmentParseResult>(new SegmentParseResult(true, context.Segment.ToString())));

            mockHandler_1
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (cntx, next) =>
                {
                    paramz_1 = cntx.Parameters;
                    return await next();
                });

            mockHandler_2
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (cntx, next) =>
                {
                    paramz_2 = cntx.Parameters;
                    return s_response;
                });

            TestRouter router = _routerBuilder
                .AddSegmentParser("int", mockIntParser.Object)
                .AddSegmentParser("str", mockStringParser.Object)
                .WithBase("/api/users/", bldr => bldr
                    .AddHandler("GET", "/{prefix:str}/{user_id:int}/dosomething", mockHandler_1.Object)
                    .AddHandler("GET", "/{prefix:str}/{user_id_str:str}/dosomething", mockHandler_2.Object))
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/api/users/whatev/1986/dosomething");

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>
            (
                () => router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object)
            )!;

            Assert.That(ex.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(paramz_1, Is.EqualTo(new Dictionary<string, object> { ["prefix"] = "whatev", ["user_id"] = 1986 }));
            Assert.That(paramz_2, Is.Null);
            mockIntParser.Verify(parser => parser.Invoke(It.Is<SegmentParserContext>(context => context.Segment.ToString() == "1986")), Times.Once);
            mockStringParser.Verify(parser => parser.Invoke(It.Is<SegmentParserContext>(context => context.Segment.ToString() == "whatev")), Times.Once);
            mockStringParser.Verify(parser => parser.Invoke(It.Is<SegmentParserContext>(context => context.Segment.ToString() == "1986")), Times.Never);
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
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/users/~denes", mockHandler.Object)
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
                .AddHandler("GET", "/files/a%20b", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/files/a%20b");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldPassRawSegmentsToSyncSegmentParsers()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            int parserCalls = 0;

            bool ParseSegment(ReadOnlyMemory<char> segment, object? _, out object? parsed)
            {
                parserCalls++;
                Assert.That(segment.ToString(), Is.EqualTo("a%20b"));
                parsed = segment.ToString();
                return true;
            }

            mockHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["name"], "a%20b")),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddSegmentParser("str", (SyncSegmentParserDelegate) ParseSegment)
                .AddHandler("GET", "/files/{name:str}", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/files/a%20b");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            Assert.That(parserCalls, Is.EqualTo(1));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldPassDecodedSegmentsToAsyncSegmentParsers()
        {
            Mock<SegmentParserDelegate> mockParser = new(MockBehavior.Strict);
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            Mock<IServiceProvider> mockServices = new(MockBehavior.Strict);

            mockParser
                .Setup(p => p.Invoke(It.Is<SegmentParserContext>(ctx => ctx.Segment.ToString() == "a%20b" && ctx.Services == mockServices.Object)))
                .Returns((SegmentParserContext context) =>
                {
                    Assert.That(context.DecodedSegment.ToString(), Is.EqualTo("a b"));
                    return new ValueTask<SegmentParseResult>(new SegmentParseResult(true, context.DecodedSegment.ToString()));
                });

            mockHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["name"], "a b")),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddSegmentParser("str", mockParser.Object)
                .AddHandler("GET", "/files/{name:str}", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/files/a%20b");

            Assert.That(await router.Handle(_request, mockServices.Object), Is.EqualTo(s_response));
            mockParser.Verify(p => p.Invoke(It.Is<SegmentParserContext>(ctx => ctx.Segment.ToString() == "a%20b" && ctx.Services == mockServices.Object)), Times.Once);
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldPassBoundParserArgumentsToSegmentParsers()
        {
            (int Min, string Text) boundArguments = (3, "it's okay");

            Mock<BindArgumentsDelegate> mockBindArguments = new(MockBehavior.Strict);
            Mock<SegmentParserDelegate> mockParser = new(MockBehavior.Strict);

            mockBindArguments
                .Setup(b => b.Invoke(It.Is<IReadOnlyDictionary<string, string>>(args =>
                    args.Count == 2 &&
                    args["min"] == "3" &&
                    args["text"] == "it's okay")))
                .Returns(boundArguments);

            mockParser
                .Setup(p => p.Invoke(It.Is<SegmentParserContext>(ctx => ctx.Segment.ToString() == "abcd" && Equals(ctx.Arguments, boundArguments))))
                .Returns((SegmentParserContext ctx) => new ValueTask<SegmentParseResult>(new SegmentParseResult(true, ctx.Segment.ToString())));

            TestRouter router = _routerBuilder
                .AddSegmentParser
                (
                    "bounded",
                    mockBindArguments.Object,
                    mockParser.Object
                )
                .AddHandler("GET", "/files/{name:bounded(min=3,text='it\\'s okay')}", async (context, _) => new HttpResponseMessage { Content = new StringContent((string) context.Parameters["name"]!) })
                .CreateRouter();

            HttpRequestMessage request = new() { Method = HttpMethod.Get, RequestUri = new Uri("https://www.exmaple.com/files/abcd") };

            HttpResponseMessage
                response1 = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object),
                response2 = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object);

            Assert.That(await response1.Content.ReadAsStringAsync(), Is.EqualTo("abcd"));
            Assert.That(await response2.Content.ReadAsStringAsync(), Is.EqualTo("abcd"));
            mockBindArguments.Verify(b => b.Invoke(It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Once);
            mockParser.Verify(p => p.Invoke(It.IsAny<SegmentParserContext>()), Times.Exactly(2));
        }

        [Test]
        public async Task Handle_ShouldPassBoundParserArgumentsToSynchronousSegmentParsers()
        {
            (int Min, string Text) boundArguments = (3, "it's okay");

            Mock<BindArgumentsDelegate> mockBindArguments = new(MockBehavior.Strict);
            int parserCalls = 0;

            mockBindArguments
                .Setup(b => b.Invoke(It.Is<IReadOnlyDictionary<string, string>>(args =>
                    args.Count == 2 &&
                    args["min"] == "3" &&
                    args["text"] == "it's okay")))
                .Returns(boundArguments);

            bool ParseBounded(ReadOnlyMemory<char> segment, object? args, out object? value)
            {
                parserCalls++;
                Assert.That(segment.ToString(), Is.EqualTo("abcd"));
                Assert.That(args, Is.EqualTo(boundArguments));
                value = segment.ToString();
                return true;
            }

            TestRouter router = _routerBuilder
                .AddSegmentParser
                (
                    "bounded",
                    mockBindArguments.Object,
                    (SyncSegmentParserDelegate) ParseBounded
                )
                .AddHandler("GET", "/files/{name:bounded(min=3,text='it\\'s okay')}", async (context, _) => new HttpResponseMessage { Content = new StringContent((string) context.Parameters["name"]!) })
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/files/abcd");

            HttpResponseMessage
                response1 = await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object),
                response2 = await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object);

            Assert.That(await response1.Content.ReadAsStringAsync(), Is.EqualTo("abcd"));
            Assert.That(await response2.Content.ReadAsStringAsync(), Is.EqualTo("abcd"));
            mockBindArguments.Verify(b => b.Invoke(It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Once);
            Assert.That(parserCalls, Is.EqualTo(2));
        }

        [Test]
        public async Task AddIntParser_ShouldRespectMinAndMaxParameters()
        {
            Mock<RequestHandlerDelegate>
                boundedHandler = new(MockBehavior.Strict),
                fallbackHandler = new(MockBehavior.Strict);

            boundedHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["value"], 15)),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            fallbackHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["value"], 25)),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddIntParser()
                .AddHandler("GET", "/items/{value:int(min=10,max=20)}", boundedHandler.Object)
                .AddHandler("GET", "/items/{value:int}", fallbackHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/items/15");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));

            _request.RequestUri = new Uri("https://www.exmaple.com/items/25");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));

            boundedHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
            fallbackHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task AddStringParser_ShouldRespectMinMaxAndPatternParameters()
        {
            Mock<RequestHandlerDelegate>
                constrainedHandler = new(MockBehavior.Strict),
                fallbackHandler = new(MockBehavior.Strict);

            constrainedHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["slug"], "abc")),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            fallbackHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["slug"], "abcd")),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddStringParser()
                .AddHandler("GET", "/tags/{slug:str(min=3,max=3,pattern='^[a-z]+$')}", constrainedHandler.Object)
                .AddHandler("GET", "/tags/{slug:str}", fallbackHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/tags/abc");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));

            _request.RequestUri = new Uri("https://www.exmaple.com/tags/abcd");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));

            constrainedHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
            fallbackHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [TestCase("/items/{value:int}")]
        [TestCase("/items/{value:int()}")]
        public async Task AddIntParser_ShouldAcceptRoutesWithoutParameters(string pattern)
        {
            Mock<RequestHandlerDelegate> handler = new(MockBehavior.Strict);
            handler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["value"], 12)), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddIntParser()
                .AddHandler("GET", pattern, handler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/items/12");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            handler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [TestCase("/items/{value:guid}")]
        [TestCase("/items/{value:guid()}")]
        public async Task AddGuidParser_ShouldAcceptRoutesWithoutParameters(string pattern)
        {
            Guid id = Guid.Parse("4a91f2c0-0e3c-4ec8-9f8c-8d2d2f2c7d1a");

            Mock<RequestHandlerDelegate> handler = new(MockBehavior.Strict);
            handler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["value"], id)), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddGuidParser()
                .AddHandler("GET", pattern, handler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri($"https://www.exmaple.com/items/{id}");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            handler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [TestCase("/flags/{value:bool}")]
        [TestCase("/flags/{value:bool()}")]
        public async Task AddBoolParser_ShouldAcceptRoutesWithoutParameters(string pattern)
        {
            Mock<RequestHandlerDelegate> handler = new(MockBehavior.Strict);
            handler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["value"], true)), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddBoolParser()
                .AddHandler("GET", pattern, handler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/flags/true");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            handler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [TestCase("/tags/{value:str}")]
        [TestCase("/tags/{value:str()}")]
        public async Task AddStringParser_ShouldAcceptRoutesWithoutParameters(string pattern)
        {
            Mock<RequestHandlerDelegate> handler = new(MockBehavior.Strict);
            handler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["value"], "tag")), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddStringParser()
                .AddHandler("GET", pattern, handler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/tags/tag");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            handler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldNormalizeDotSegmentsInUriPaths()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddHandler("GET", "/users/denes", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/users/../users/denes");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldContinueMatchingWhenASegmentParserReturnsFalse()
        {
            Mock<RequestHandlerDelegate>
                mockIntHandler = new(MockBehavior.Strict),
                mockStringHandler = new(MockBehavior.Strict);

            int intParserCalls = 0, stringParserCalls = 0;

            bool ParseInt(ReadOnlyMemory<char> segment, object? _, out object? parsed)
            {
                intParserCalls++;
                Assert.That(segment.ToString(), Is.EqualTo("abc"));
                parsed = null;
                return false;
            }

            bool ParseString(ReadOnlyMemory<char> segment, object? _, out object? parsed)
            {
                stringParserCalls++;
                Assert.That(segment.ToString(), Is.EqualTo("abc"));
                parsed = segment.ToString();
                return true;
            }

            mockStringHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["slug"], "abc")),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddSegmentParser("int", (SyncSegmentParserDelegate) ParseInt)
                .AddSegmentParser("str", (SyncSegmentParserDelegate) ParseString)
                .WithBase("/api/", bldr => bldr
                    .AddHandler("GET", "/{id:int}/details", mockIntHandler.Object)
                    .AddHandler("GET", "/{slug:str}/details", mockStringHandler.Object))
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/api/abc/details");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockIntHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Never);
            mockStringHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
            Assert.That(intParserCalls, Is.EqualTo(1));
            Assert.That(stringParserCalls, Is.EqualTo(1));
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
                Assert.That(requestStarted.PayloadNames, Is.EquivalentTo(new[] { "RequestUri", "Verb" }));
                Assert.That(requestStarted.Payload, Is.EquivalentTo(new object?[] { "https://www.exmaple.com/path/to/somewhere", HttpVerb.Get }));

                Assert.That(matchingHandler.Level, Is.EqualTo(EventLevel.Informational));
                Assert.That(matchingHandler.PayloadNames, Is.EquivalentTo(new[] { "RequestUri", "Verb", "Pattern", "ParameterCount" }));
                Assert.That(matchingHandler.Payload, Is.EquivalentTo(new object?[] { "https://www.exmaple.com/path/to/somewhere", HttpVerb.Get, "/path/to/somewhere", 0 }));
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
                Assert.That(requestStarted.PayloadNames, Is.EquivalentTo(new[] { "RequestUri", "Verb" }));
                Assert.That(requestStarted.Payload, Is.EquivalentTo(new object?[] { "https://www.exmaple.com/path/to/nowhere", HttpVerb.Get }));

                Assert.That(noMatchingHandler.Level, Is.EqualTo(EventLevel.Informational));
                Assert.That(noMatchingHandler.PayloadNames, Is.EquivalentTo(new[] { "RequestUri", "Verb" }));
                Assert.That(noMatchingHandler.Payload, Is.EquivalentTo(new object?[] { "https://www.exmaple.com/path/to/nowhere", HttpVerb.Get }));
            });
        }
    }
}
