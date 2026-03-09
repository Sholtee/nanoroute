/********************************************************************************
* RouterTests.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

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
    }
}
