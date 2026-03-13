/********************************************************************************
* RouterTests.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;

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
        private static readonly object s_request = new();

        private Mock<Router<object, object>> _mockRouter = null!;

        private DebugEventListener _debugEventListener = null!;

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
            _mockRouter = new Mock<Router<object, object>>(MockBehavior.Strict);
            _mockRouter
                .Protected()
                .Setup<string>("GetVerb", ItExpr.IsAny<object>())
                .Returns("GET");
        }

        [Test]
        public void Handle_ShouldMatchTheShortestPrefix()
        {
            Mock<RequestHandler<object, object>>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict),
                mockHandler_3 = new(MockBehavior.Strict);

            MockSequence seq = new();

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((_, next) => next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((_, next) => next());

            mockHandler_3
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns(true);

            _mockRouter.Object
                .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                .AddHandler("GET", "/path/to/{some_str:any}/something/", mockHandler_3.Object) // should match 3rd
                .AddHandler("GET", "/path/to/explicit/something/", mockHandler_2.Object)  // should match 2nd
                .AddHandler("GET", "/", mockHandler_1.Object)  // should match 1st
                .AddHandler("GET", "/path/should/not/match/", new Mock<RequestHandler<object, object>>(MockBehavior.Strict).Object);

            _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request)
                .Returns(new Uri("https://www.exmaple.com/path/to/explicit/something/"));

            Assert.That(_mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
        }

        [Test]
        public void Handle_ShouldSupportExactMatches()
        {
            Mock<RequestHandler<object, object>> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns(true);

            _mockRouter.Object.AddHandler("GET", "/path/to/explicit/something", mockHandler.Object);

            ISetup<Router<object, object>, Uri> getUriSetup = _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request);

            getUriSetup.Returns(new Uri("https://www.exmaple.com/path/to/explicit/something/cica"));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext<object>>(), It.IsAny<Func<object>>()), Times.Never);

            getUriSetup.Returns(new Uri("https://www.exmaple.com/path/to/explicit/something/"));
            Assert.That(_mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()), Times.Once);
        }

        [Test]
        public void Handle_ShouldSupportPrefixes()
        {
            Mock<RequestHandler<object, object>> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns(true);

            _mockRouter.Object.AddHandler("GET", "/path/to/explicit/something/", mockHandler.Object);

            ISetup<Router<object, object>, Uri> getUriSetup = _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request);

            getUriSetup.Returns(new Uri("https://www.exmaple.com/path/to/explicit/something/cica"));

            Assert.That(_mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext<object>>(), It.IsAny<Func<object>>()), Times.Once);

            getUriSetup.Returns(new Uri("https://www.exmaple.com/path/to/explicit/something/"));
            Assert.That(_mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()), Times.Exactly(2));
        }

        [Test]
        public void Handle_ShouldSupportMultipleHandlersAgainstTheSamePattern([Values("/path/to/explicit/something", "/path/to/{some_str:any}/something")] string pattern)
        {
            MockSequence seq = new();

            Mock<RequestHandler<object, object>>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((_, next) => next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns(true);

            _mockRouter.Object
                .AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; })
                .AddHandler("GET", pattern, mockHandler_1.Object)
                .AddHandler("GET", pattern, mockHandler_2.Object);

            _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request)
                .Returns(new Uri("https://www.exmaple.com/path/to/explicit/something"));

            Assert.That(_mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler_1.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()), Times.Once);
            mockHandler_2.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()), Times.Once);
        }

        [Test]
        public void ExactMatch_ShouldHaveThePriority([Values] bool explicitFirst)
        {
            MockSequence seq = new();

            Mock<RequestHandler<object, object>>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            mockHandler_1
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((_, next) => next());

            mockHandler_2
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns(true);

            _mockRouter.Object.AddParameterParser("any", (string segment, out object? parsed) => { parsed = segment; return true; });

            if (explicitFirst)
                _mockRouter.Object
                    .AddHandler("GET", "/path/to/explicit/something", mockHandler_1.Object)
                    .AddHandler("GET", "/path/to/{some_str:any}/something", mockHandler_2.Object);
            else
                _mockRouter.Object
                    .AddHandler("GET", "/path/to/{some_str:any}/something", mockHandler_2.Object)
                    .AddHandler("GET", "/path/to/explicit/something", mockHandler_1.Object);

            _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request)
                .Returns(new Uri("https://www.exmaple.com/path/to/explicit/something"));

            Assert.That(_mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler_1.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()), Times.Once);
            mockHandler_2.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()), Times.Once);
        }

        [Test]
        public void Handlers_MayShareData()
        {
            MockSequence seq = new();

            Mock<RequestHandler<object, object>>
                mockGetUser = new(MockBehavior.Strict),
                mockDoSomethingWithUser = new(MockBehavior.Strict);

            mockGetUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("user_id"));
                    Assert.That(cntx.Parameters["user_id"], Is.EqualTo(1986));

                    cntx.Parameters["User"] = new object();  // user object
                    return next();
                });

            mockDoSomethingWithUser
                .InSequence(seq)
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((cntx, next) =>
                {
                    Assert.That(cntx.Parameters, Does.ContainKey("User"));
                    Assert.That(cntx.Parameters["User"], Is.InstanceOf<object>());

                    return true;
                });

            _mockRouter.Object
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
                .AddHandler("GET", "api/users/{user_id:int}/dosomething", mockDoSomethingWithUser.Object);

            _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request)
                .Returns(new Uri("https://www.exmaple.com/api/users/1986/dosomething"));

            Assert.That(_mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockGetUser.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()), Times.Once);
            mockDoSomethingWithUser.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()), Times.Once);
        }

        [Test]
        public void Handler_ShouldBeBoundToVerb()
        {
            Mock<RequestHandler<object, object>> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns(true);

            _mockRouter.Object.AddHandler("POST", "path/to/somewhere", mockHandler.Object);

            _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request)
                .Returns(new Uri("https://www.exmaple.com/path/to/somewhere"));

            ISetup<Router<object, object>, string> getVerbSetup = _mockRouter
                .Protected()
                .Setup<string>("GetVerb", s_request);

            getVerbSetup.Returns("GET");

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext<object>>(), It.IsAny<Func<object>>()), Times.Never);

            getVerbSetup.Returns("POST");

            Assert.That(_mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()), Times.Once);
        }

        [Test]
        public void Handler_ShouldHandleMultipleVerbs()
        {
            Mock<RequestHandler<object, object>> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns(true);

            _mockRouter.Object.AddHandler(["GET", "POST"], "path/to/somewhere", mockHandler.Object);

            _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request)
                .Returns(new Uri("https://www.exmaple.com/path/to/somewhere"));

            ISetup<Router<object, object>, string> getVerbSetup = _mockRouter
                .Protected()
                .Setup<string>("GetVerb", s_request);

            getVerbSetup.Returns("GET");

            Assert.That(_mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()), Times.Once);

            getVerbSetup.Returns("POST");

            Assert.That(_mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()), Times.Exactly(2));
        }

        [Test]
        public void AddHandler_ShouldCanRegisterAllVerbs()
        {
            Mock<RequestHandler<object, object>> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns(true);

            _mockRouter.Object.AddHandler("path/to/somewhere", mockHandler.Object);

            _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request)
                .Returns(new Uri("https://www.exmaple.com/path/to/somewhere"));

            ISetup<Router<object, object>, string> getVerbSetup = _mockRouter
                .Protected()
                .Setup<string>("GetVerb", s_request);

            foreach (string verb in new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "TRACE" })
            {
                getVerbSetup.Returns(verb);

                Assert.That(_mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
            }
        }

        [Test]
        public void Handle_ShouldBeCaseInsensitive()
        {
            Mock<RequestHandler<object, object>> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns(true);

            _mockRouter.Object.AddHandler("GET", "path/to/SOMEWHERE", mockHandler.Object);

            _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request)
                .Returns(new Uri("https://www.exmaple.com/PATH/to/somewhere"));

            Assert.That(_mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
        }

        [Test]
        public void AddHandler_ShouldThrowOnMissingParameterParser([Values("path/to/{missing}", "path/to/{parameter_name:missing}")] string pattern)
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _mockRouter.Object.AddHandler(pattern, new Mock<RequestHandler<object, object>>(MockBehavior.Strict).Object))!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_NO_SUCH_PARAMETER_PARSER, "missing")));
        }


        [Test]
        public void AddHandler_ShouldThrowOnInvalidVerb()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _mockRouter.Object.AddHandler("INVALID", "/path/to/somewhere", new Mock<RequestHandler<object, object>>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, "INVALID")));
        }

        [Test]
        public void AddHandler_ShouldThrowOnParameterOverride()
        {
            _mockRouter.Object
                .AddParameterParser("int", new Mock<ParameterParserDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/path/to/{id:int}", new Mock<RequestHandler<object, object>>(MockBehavior.Strict).Object);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _mockRouter.Object.AddHandler("GET", "/path/to/{other_id:int}", new Mock<RequestHandler<object, object>>(MockBehavior.Strict).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_PARAMETER_OVERRIDE));
        }

        [Test]
        public void DefaultHandler_ShouldHandleNotFoundEvents([Values] bool populateErrorInfo)
        {
            _mockRouter.Object.AddDefaultHandler(populateErrorInfo);

            _mockRouter
                .Protected()
                .Setup<object>("CreateJsonResponse", HttpStatusCode.NotFound, ItExpr.IsAny<string>())
                .Returns<HttpStatusCode, string>((_, resp) => resp);

            _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request)
                .Returns(new Uri("https://www.exmaple.com/nonexistent"));

            string resp = (string) _mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Strict).Object);
            
            JsonResponse deserialized = JsonSerializer.Deserialize(resp, JsonContext.Default.JsonResponse)!;
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Reason, Is.Null);
            Assert.That(deserialized.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));

            _mockRouter
                .Protected()
                .Verify<object>("CreateJsonResponse", Times.Once(), HttpStatusCode.NotFound, ItExpr.IsAny<string>());
        }

        [Test]
        public void DefaultHandler_ShouldHandleInternalErrors([Values] bool populateErrorInfo)
        {
            const string ERROR_MSG = "Oooops";

            _mockRouter.Object.AddDefaultHandler(populateErrorInfo);

            _mockRouter
                .Protected()
                .Setup<object>("CreateJsonResponse", HttpStatusCode.InternalServerError, ItExpr.IsAny<string>())
                .Returns<HttpStatusCode, string>((_, resp) => resp);

            _mockRouter.Object.AddHandler("GET", "/somewhere", (_, _) => throw new Exception(ERROR_MSG));

            _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request)
                .Returns(new Uri("https://www.exmaple.com/somewhere"));

            string resp = (string) _mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Strict).Object);

            JsonResponse deserialized = JsonSerializer.Deserialize(resp, JsonContext.Default.JsonResponse)!;
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Reason, populateErrorInfo ? Does.Contain(ERROR_MSG) : Is.Null);
            Assert.That(deserialized.Message, Is.EqualTo(Resources.ERR_INERNAL_ERROR));

            _mockRouter
                .Protected()
                .Verify<object>("CreateJsonResponse", Times.Once(), HttpStatusCode.InternalServerError, ItExpr.IsAny<string>());
        }

        [Test]
        public void DefaultHandler_ShouldLetNormalWorkflowGo()
        {
            _mockRouter.Object
                .AddDefaultHandler()
                .AddHandler("GET", "/somewhere", (_, _) => true);

            _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request)
                .Returns(new Uri("https://www.exmaple.com/somewhere"));

            Assert.That(_mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
        }

        [Test]
        public void AddParameterParser_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.AddParameterParser(null!, new Mock<ParameterParserDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("parserName"));

            ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.AddParameterParser("any", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("tryParseDelegate"));
        });

        [Test]
        public void AddHandler_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.AddHandler(null!, (_, _) => new object()))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.AddHandler("path", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.AddHandler((IEnumerable<string>) null!, "path", (_, _) => new object()))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.AddHandler((string) null!, "path", (_, _) => new object()))!;
            Assert.That(ex.ParamName, Is.EqualTo("verb"));

            ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.AddHandler(["GET"], null!, (_, _) => new object()))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.AddHandler(["GET"], "path", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));

            ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.AddHandler("GET", null!, (_, _) => new object()))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));

            ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.AddHandler("GET", "path", null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("handler"));
        });

        [Test]
        public void Handle_ShouldBeNullChecked()
        {
            Assert.Multiple(() =>
            {
                ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.Handle(null!, new Mock<IServiceProvider>(MockBehavior.Strict).Object))!;
                Assert.That(ex.ParamName, Is.EqualTo("request"));

                ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.Handle(s_request, null!))!;
                Assert.That(ex.ParamName, Is.EqualTo("services"));
            });
        }

        [Test]
        public void Parameters_ShouldNotLeak()
        {
            Mock<RequestHandler<object, object>>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            Dictionary<string, object?>
                paramz_1 = null!,
                paramz_2 = null!;

            mockHandler_1
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((cntx, next) =>
                {
                    paramz_1 = cntx.Parameters;
                    return next();
                });

            mockHandler_2
                .Setup(h => h.Invoke(It.Is<RequestContext<object>>(c => c.Request == s_request), It.IsAny<Func<object>>()))
                .Returns<RequestContext<object>, Func<object>>((cntx, next) =>
                {
                    paramz_2 = cntx.Parameters;
                    return true;
                });

            _mockRouter.Object
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
                .AddHandler("GET", "api/users/{prefix:str}/{user_id_str:str}/dosomething", mockHandler_2.Object);

            _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_request)
                .Returns(new Uri("https://www.exmaple.com/api/users/whatev/1986/dosomething"));

            _mockRouter.Object.Handle(s_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object);

            Assert.That(paramz_1, Is.EqualTo(new Dictionary<string, object> { ["prefix"] = "whatev", ["user_id"] = 1986 }));
            Assert.That(paramz_2, Is.EqualTo(new Dictionary<string, object> { ["prefix"] = "whatev", ["user_id_str"] = "1986" }));
        }
    }
}
