/********************************************************************************
* RouterTests.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

using Moq;
using Moq.Language.Flow;
using Moq.Protected;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Properties;

    [TestFixture]
    internal sealed class RouterTests
    {
        private static Mock<Router<object, object>> CreateRouter(MatchingStrategy matchingStrategy)
        {
            Mock<Router<object, object>> mockRouter = new (MockBehavior.Strict, matchingStrategy);
            mockRouter
                .Protected()
                .Setup<string>("GetRequestId", ItExpr.IsAny<object>())
                .Returns("requestId");
            mockRouter
                .Protected()
                .Setup<HttpVerb>("GetVerb", ItExpr.IsAny<object>())
                .Returns(HttpVerb.Get);

            return mockRouter;
        }

        [Test]
        public void Handle_ShouldMatchTheShortestPrefix()
        {
            Mock<RequestHandler<object, object>>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict),
                mockHandler_3 = new(MockBehavior.Strict);

            object request = new();

            MockSequence seq = new();

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((_, next) => next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((_, next) => next());

            mockHandler_3
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns(true);

            Mock<Router<object, object>> mockRouter = CreateRouter(MatchingStrategy.ShortestPrefixMatching);

            mockRouter.Object
                .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                .AddHandler(HttpVerb.Get, "/path/to/{some_str:any}/something/", mockHandler_3.Object) // should match 3rd
                .AddHandler(HttpVerb.Get, "/path/to/explicit/something/", mockHandler_2.Object)  // should match 2nd
                .AddHandler(HttpVerb.Get, "/", mockHandler_1.Object)  // should match 1st
                .AddHandler(HttpVerb.Get, "/path/should/not/match/", new Mock<RequestHandler<object, object>>(MockBehavior.Strict).Object);

            mockRouter
                .Protected()
                .Setup<Uri>("GetUri", request)
                .Returns(new Uri("https://www.exmaple.com/path/to/explicit/something/"));

            Assert.That(mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
        }

        [Test]
        public void Handle_ShouldMatchInTheRegistrationOrder()
        {
            Mock<RequestHandler<object, object>>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict),
                mockHandler_3 = new(MockBehavior.Strict);

            object request = new();

            MockSequence seq = new();

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((_, next) => next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((_, next) => next());

            mockHandler_3
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns(true);

            Mock<Router<object, object>> mockRouter = CreateRouter(MatchingStrategy.RegistrationOrderMatching);

            mockRouter.Object
                .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                .AddHandler(HttpVerb.Get, "/path/to/{some_str:any}/something/", mockHandler_1.Object)
                .AddHandler(HttpVerb.Get, "/path/to/explicit/something/", mockHandler_2.Object)
                .AddHandler(HttpVerb.Get, "/", mockHandler_3.Object)
                .AddHandler(HttpVerb.Get, "/path/should/not/match/", new Mock<RequestHandler<object, object>>(MockBehavior.Strict).Object);

            mockRouter
                .Protected()
                .Setup<Uri>("GetUri", request)
                .Returns(new Uri("https://www.exmaple.com/path/to/explicit/something/"));

            Assert.That(mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
        }

        [Test]
        public void Handle_ShouldSupportExactMatches([Values] MatchingStrategy matchingStrategy)
        {
            object request = new();

            Mock<RequestHandler<object, object>> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns(true);

            Mock<Router<object, object>> mockRouter = CreateRouter(matchingStrategy);

            mockRouter.Object.AddHandler(HttpVerb.Get, "/path/to/explicit/something", mockHandler.Object);

            ISetup<Router<object, object>, Uri> getUriSetup = mockRouter
                .Protected()
                .Setup<Uri>("GetUri", request);

            getUriSetup.Returns(new Uri("https://www.exmaple.com/path/to/explicit/something/cica"));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext<object>>(), It.IsAny<Func<object>>()), Times.Never);

            getUriSetup.Returns(new Uri("https://www.exmaple.com/path/to/explicit/something/"));
            Assert.That(mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()), Times.Once);
        }

        [Test]
        public void Handle_ShouldSupportPrefixes([Values] MatchingStrategy matchingStrategy)
        {
            object request = new();

            Mock<RequestHandler<object, object>> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns(true);

            Mock<Router<object, object>> mockRouter = CreateRouter(matchingStrategy);

            mockRouter.Object.AddHandler(HttpVerb.Get, "/path/to/explicit/something/", mockHandler.Object);

            ISetup<Router<object, object>, Uri> getUriSetup = mockRouter
                .Protected()
                .Setup<Uri>("GetUri", request);

            getUriSetup.Returns(new Uri("https://www.exmaple.com/path/to/explicit/something/cica"));

            Assert.That(mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext<object>>(), It.IsAny<Func<object>>()), Times.Once);

            getUriSetup.Returns(new Uri("https://www.exmaple.com/path/to/explicit/something/"));
            Assert.That(mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()), Times.Exactly(2));
        }

        [Test]
        public void Handle_ShouldSupportMultipleHandlersAgainstTheSamePattern([Values] MatchingStrategy matchingStrategy, [Values("/path/to/explicit/something", "/path/to/{some_str:any}/something")] string pattern)
        {
            Mock<Router<object, object>> mockRouter = CreateRouter(matchingStrategy);

            object request = new();

            MockSequence seq = new();

            Mock<RequestHandler<object, object>>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((_, next) => next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns(true);

            mockRouter.Object
                .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                .AddHandler(HttpVerb.Get, pattern, mockHandler_1.Object)
                .AddHandler(HttpVerb.Get, pattern, mockHandler_2.Object);

            mockRouter
                .Protected()
                .Setup<Uri>("GetUri", request)
                .Returns(new Uri("https://www.exmaple.com/path/to/explicit/something"));

            Assert.That(mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler_1.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()), Times.Once);
            mockHandler_2.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()), Times.Once);
        }

        [Test]
        public void Handlers_MayShareData()
        {
            Mock<Router<object, object>> mockRouter = CreateRouter(MatchingStrategy.ShortestPrefixMatching);

            object request = new();

            MockSequence seq = new();

            Mock<RequestHandler<object, object>>
                mockGetUser = new(MockBehavior.Strict),
                mockDoSomethingWithUser = new(MockBehavior.Strict);

            mockGetUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("user_id"));
                    Assert.That(cntx.Parameters["user_id"], Is.EqualTo(1986));

                    cntx.Parameters["User"] = new object();  // user object
                    return next();
                });

            mockDoSomethingWithUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("User"));
                    Assert.That(cntx.Parameters["User"], Is.InstanceOf<object>());

                    return true;
                });

            mockRouter.Object
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
                .AddHandler(HttpVerb.Get, "api/users/{user_id:int}/", mockGetUser.Object)
                .AddHandler(HttpVerb.Get, "api/users/{user_id:int}/dosomething", mockDoSomethingWithUser.Object);

            mockRouter
                .Protected()
                .Setup<Uri>("GetUri", request)
                .Returns(new Uri("https://www.exmaple.com/api/users/1986/dosomething"));

            Assert.That(mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockGetUser.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()), Times.Once);
            mockDoSomethingWithUser.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()), Times.Once);
        }

        [Test]
        public void Handler_ShouldBeBoundToVerb()
        {
            object request = new();

            Mock<Router<object, object>> mockRouter = CreateRouter(MatchingStrategy.ShortestPrefixMatching);

            Mock<RequestHandler<object, object>> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns(true);

            mockRouter.Object.AddHandler(HttpVerb.Post, "path/to/somewhere", mockHandler.Object);

            mockRouter
                .Protected()
                .Setup<Uri>("GetUri", request)
                .Returns(new Uri("https://www.exmaple.com/path/to/somewhere"));

            ISetup<Router<object, object>, HttpVerb> getVerbSetup = mockRouter
                .Protected()
                .Setup<HttpVerb>("GetVerb", request);

            getVerbSetup.Returns(HttpVerb.Get);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext<object>>(), It.IsAny<Func<object>>()), Times.Never);

            getVerbSetup.Returns(HttpVerb.Post);

            Assert.That(mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()), Times.Once);
        }

        [Test]
        public void Handler_ShouldHandleMultipleVerbs()
        {
            object request = new();

            Mock<Router<object, object>> mockRouter = CreateRouter(MatchingStrategy.ShortestPrefixMatching);

            Mock<RequestHandler<object, object>> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns(true);

            mockRouter.Object.AddHandler([HttpVerb.Get, HttpVerb.Post], "path/to/somewhere", mockHandler.Object);

            mockRouter
                .Protected()
                .Setup<Uri>("GetUri", request)
                .Returns(new Uri("https://www.exmaple.com/path/to/somewhere"));

            ISetup<Router<object, object>, HttpVerb> getVerbSetup = mockRouter
                .Protected()
                .Setup<HttpVerb>("GetVerb", request);

            getVerbSetup.Returns(HttpVerb.Get);

            Assert.That(mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()), Times.Once);

            getVerbSetup.Returns(HttpVerb.Post);

            Assert.That(mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()), Times.Exactly(2));
        }

        [Test]
        public void AddHandler_ShouldCanRegisterAllVerbs()
        {
            object request = new();

            Mock<Router<object, object>> mockRouter = CreateRouter(MatchingStrategy.ShortestPrefixMatching);

            Mock<RequestHandler<object, object>> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns(true);

            mockRouter.Object.AddHandler("path/to/somewhere", mockHandler.Object);

            mockRouter
                .Protected()
                .Setup<Uri>("GetUri", request)
                .Returns(new Uri("https://www.exmaple.com/path/to/somewhere"));

            ISetup<Router<object, object>, HttpVerb> getVerbSetup = mockRouter
                .Protected()
                .Setup<HttpVerb>("GetVerb", request);

            foreach (HttpVerb verb in HttpVerb.GetValues())
            {
                getVerbSetup.Returns(verb);

                Assert.That(mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            }
        }

        [Test]
        public void Handle_ShouldBeCaseInsensitive()
        {
            object request = new();

            Mock<Router<object, object>> mockRouter = CreateRouter(MatchingStrategy.ShortestPrefixMatching);

            Mock<RequestHandler<object, object>> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns(true);

            mockRouter.Object.AddHandler(HttpVerb.Get, "path/to/SOMEWHERE", mockHandler.Object);

            mockRouter
                .Protected()
                .Setup<Uri>("GetUri", request)
                .Returns(new Uri("https://www.exmaple.com/PATH/to/somewhere"));

            Assert.That(mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
        }

        [Test]
        public void AddHandler_ShouldThrowOnMissingParameterParser([Values("path/to/{missing}", "path/to/{parameter_name:missing}")] string pattern)
        {
            Mock<Router<object, object>> mockRouter = CreateRouter(MatchingStrategy.ShortestPrefixMatching);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => mockRouter.Object.AddHandler(pattern, new Mock<RequestHandler<object, object>>(MockBehavior.Strict).Object))!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_NO_SUCH_PARAMETER_PARSER, "missing")));
        }

        [Test]
        public void Parameters_ShouldNotLeak()
        {
            object request = new();

            Mock<Router<object, object>> mockRouter = CreateRouter(MatchingStrategy.ShortestPrefixMatching);

            Mock<RequestHandler<object, object>>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            Dictionary<string, object?>
                paramz_1 = null!,
                paramz_2 = null!;

            mockHandler_1
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((cntx, next) =>
                {
                    paramz_1 = cntx.Parameters;
                    return next();
                });

            mockHandler_2
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((cntx, next) =>
                {
                    paramz_2 = cntx.Parameters;
                    return true;
                });

            mockRouter.Object
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
                .AddHandler(HttpVerb.Get, "api/users/{prefix:str}/{user_id:int}/dosomething", mockHandler_1.Object)
                .AddHandler(HttpVerb.Get, "api/users/{prefix:str}/{user_id_str:str}/dosomething", mockHandler_2.Object);

            mockRouter
                .Protected()
                .Setup<Uri>("GetUri", request)
                .Returns(new Uri("https://www.exmaple.com/api/users/whatev/1986/dosomething"));

            mockRouter.Object.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object);

            Assert.That(paramz_1, Is.EqualTo(new Dictionary<string, object> { ["prefix"] = "whatev", ["user_id"] = 1986 }));
            Assert.That(paramz_2, Is.EqualTo(new Dictionary<string, object> { ["prefix"] = "whatev", ["user_id_str"] = "1986" }));
        }
    }
}
