/********************************************************************************
* RouterBuilderTests.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class RouterBuilderTests
    {
        private sealed class TestRouter : RoutingContext { } 

        private RouterBuilder<TestRouter> _routerBuilder = null!;

        private Mock<Action<TestRouter>> _mockConfigureRouter = null!;

        [SetUp]
        public void Setup()
        {
            _mockConfigureRouter = new Mock<Action<TestRouter>>(MockBehavior.Strict);

            _routerBuilder = new RouterBuilder<TestRouter>(_mockConfigureRouter.Object);
        }

        [Test]
        public void CreateRouter_ShouldCopyTheRootNode()
        {
            Mock<RequestHandler> mockRequestHandler = new(MockBehavior.Strict);

            _routerBuilder.AddHandler("GET", "", mockRequestHandler.Object);
            Assert.That(_routerBuilder.Root.HandlerRegistrations[HttpVerb.Get], Has.Count.EqualTo(1));
            Assert.That(_routerBuilder.Root.HandlerRegistrations[HttpVerb.Get].Single().Handler, Is.SameAs(mockRequestHandler.Object));

            TestRouter router = _routerBuilder.CreateRouter();
            Assert.That(router.Root, Is.Not.SameAs(_routerBuilder.Root));
            Assert.That(router.Root.HandlerRegistrations[HttpVerb.Get], Has.Count.EqualTo(1));
            Assert.That(router.Root.HandlerRegistrations[HttpVerb.Get].Single().Handler, Is.SameAs(mockRequestHandler.Object));
        }

        [Test]
        public void WithBase_ShouldInheritTheParentParameterParsers()
        {
            // TODO
        }

        /*

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

        private sealed record JsonResponse(string Message, string? Reason);

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
                .Setup<Uri>("GetUri", s_converted_request)
                .Returns(new Uri("https://www.exmaple.com/nonexistent"));

            string resp = (string)_mockRouter.Object.Handle(s_converted_request, new Mock<IServiceProvider>(MockBehavior.Strict).Object);

            JsonResponse deserialized = JsonSerializer.Deserialize<JsonResponse>(resp)!;
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
                .Setup<Uri>("GetUri", s_converted_request)
                .Returns(new Uri("https://www.exmaple.com/somewhere"));

            string resp = (string)_mockRouter.Object.Handle(s_converted_request, new Mock<IServiceProvider>(MockBehavior.Strict).Object);

            JsonResponse deserialized = JsonSerializer.Deserialize<JsonResponse>(resp)!;
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
                .Setup<Uri>("GetUri", s_converted_request)
                .Returns(new Uri("https://www.exmaple.com/somewhere"));

            Assert.That(_mockRouter.Object.Handle(s_converted_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
        }

        private static IEnumerable<TestCaseData> AddDefaultParsers_ShouldRegisterTheBuiltInParsers_Cases()
        {
            yield return new TestCaseData("int", 42);
            yield return new TestCaseData("guid", Guid.Parse("4a91f2c0-0e3c-4ec8-9f8c-8d2d2f2c7d1a"));
            yield return new TestCaseData("bool", true);
            yield return new TestCaseData("str", "spikey");
        }

        [TestCaseSource(nameof(AddDefaultParsers_ShouldRegisterTheBuiltInParsers_Cases))]
        public void AddDefaultParsers_ShouldRegisterTheBuiltInParsers(string parserName, object expectedValue)
        {
            _mockRouter.Object
                .AddDefaultParsers()
                .AddHandler("GET", $"/items/{{value:{parserName}}}", (context, _) =>
                {
                    Assert.That(context.Parameters["value"], Is.EqualTo(expectedValue));
                    return true;
                });

            _mockRouter
                .Protected()
                .Setup<Uri>("GetUri", s_converted_request)
                .Returns(new Uri($"https://www.exmaple.com/items/{expectedValue}"));

            Assert.That(_mockRouter.Object.Handle(s_converted_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.True);
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

            ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.AddHandler((IEnumerable<string>)null!, "path", (_, _) => new object()))!;
            Assert.That(ex.ParamName, Is.EqualTo("verbs"));

            ex = Assert.Throws<ArgumentNullException>(() => _mockRouter.Object.AddHandler((string)null!, "path", (_, _) => new object()))!;
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
        */
    }
}
