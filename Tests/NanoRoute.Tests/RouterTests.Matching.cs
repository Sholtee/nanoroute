/********************************************************************************
* RouterTests.Matching.cs                                                       *
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
    using Properties;

    internal sealed partial class RouterTests
    {
        [Test]
        public async Task Route_ShouldMatchTheShortestPrefix()
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
                .ReturnsAsync(s_response);

            InMemoryRouter router = _routerBuilder
                .AddValueParser("any", new Mock<SyncValueParserDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/path/to/{some_str:any}/something/*", mockHandler_3.Object) // should not match after the literal branch was selected
                .AddHandler("GET", "/path/to/explicit/something/*", mockHandler_2.Object)  // should match 2nd
                .AddHandler("GET", "/*", mockHandler_1.Object)  // should match 1st
                .AddHandler("GET", "/path/should/not/match/*", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");

            Assert.That(await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler_3.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Never);
        }

        [Test]
        public async Task Route_ShouldMatchTheShortestPrefix_BasePrefix()
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
                .ReturnsAsync(s_response);

            InMemoryRouter router = _routerBuilder
                .AddHandler("GET", "/*", mockHandler_1.Object)  // should match 1st
                .AddHandler("GET", "/path/should/not/match/*", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object)
                .AddPrefix("/path/to/*", routerBuilder => routerBuilder
                    .AddValueParser("any", new Mock<SyncValueParserDelegate>(MockBehavior.Strict).Object)
                    .AddHandler("GET", "/{some_str:any}/something/*", mockHandler_3.Object) // should not match after the literal branch was selected
                    .AddHandler("GET", "/explicit/something/*", mockHandler_2.Object))  // should match 2nd
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");

            Assert.That(await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler_3.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Never);
        }

        [Test]
        public async Task Route_ShouldSupportExactMatches()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            _routerBuilder.AddHandler("GET", "/path/to/explicit/something/", mockHandler.Object);

            InMemoryRouter router = _routerBuilder.CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/cica");
            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NOT_FOUND));
            Assert.That(ex.Data["StatusCode"], Is.EqualTo(HttpStatusCode.NotFound));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Never);

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");
            Assert.That(await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Route_ShouldSupportPrefixes()
        {
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            mockHandler
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .ReturnsAsync(s_response);

            _routerBuilder.AddHandler("GET", "/path/to/explicit/something/*", mockHandler.Object);

            InMemoryRouter router = _routerBuilder.CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/cica");
            Assert.That(await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something/");
            Assert.That(await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Exactly(2));
        }

        [Test]
        public async Task Route_ShouldExposeRemainingPathForEachMatchedHandler()
        {
            List<string> remainingPaths = [];

            InMemoryRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler("GET", "/*", async (context, next) =>
                {
                    remainingPaths.Add(context.RemainingPath.ToString());
                    return await next();
                })
                .AddHandler("GET", "/api/*", async (context, next) =>
                {
                    remainingPaths.Add(context.RemainingPath.ToString());
                    return await next();
                })
                .AddHandler("GET", "/api/users/{id:int}/", (context, _) =>
                {
                    remainingPaths.Add(context.RemainingPath.ToString());
                    return Task.FromResult(s_response);
                })
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/api/users/42?include=details");

            Assert.That(await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            Assert.That(remainingPaths, Is.EqualTo(new[] { "/api/users/42", "/users/42", string.Empty }));
        }

        [TestCase("/path/to/explicit/something/")]
        [TestCase("/path/to/{some_str:any}/something/")]
        public async Task Route_ShouldSupportMultipleHandlersAgainstTheSamePattern(string pattern)
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

            InMemoryRouter router = _routerBuilder
                .AddValueParser("any", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = segment.ToString(); return true; })
                .AddHandler("GET", pattern, mockHandler_1.Object)
                .AddHandler("GET", pattern, mockHandler_2.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/path/to/explicit/something");
            Assert.That(await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockHandler_1.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
            mockHandler_2.Verify(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [TestCase(MatchingPrecedence.LiteralFirst)]
        [TestCase(MatchingPrecedence.ParameterizedFirst)]
        public async Task Route_ShouldRespectConfiguredMatchingPrecedence(MatchingPrecedence matchingPrecedence)
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

            InMemoryRouter router = _routerBuilder
                .AddValueParser("str", (ReadOnlyMemory<char> segment, object? _, out object? parsed) =>
                {
                    parsed = segment.ToString();
                    return true;
                })
                .ConfigureRouting(config => config with { MatchingPrecedence = matchingPrecedence })
                .AddHandler("GET", "/items/literal/", mockLiteralHandler.Object)
                .AddHandler("GET", "/items/{value:str}/", mockParameterizedHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/items/literal");

            Assert.That(await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockLiteralHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), matchingPrecedence == MatchingPrecedence.LiteralFirst ? Times.Once() : Times.Never());
            mockParameterizedHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), matchingPrecedence == MatchingPrecedence.ParameterizedFirst ? Times.Once() : Times.Never());
        }

        [Test]
        public async Task Route_ShouldStayOnTheSelectedParameterizedBranch()
        {
            Mock<RequestHandlerDelegate>
                mockHandler_1 = new(MockBehavior.Strict),
                mockHandler_2 = new(MockBehavior.Strict);

            Mock<ValueParserDelegate>
                mockIntParser = new(MockBehavior.Strict),
                mockStringParser = new(MockBehavior.Strict);

            IDictionary<string, object?>
                params_1 = null!,
                params_2 = null!;

            mockIntParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "1986")))
                .Returns(new ValueTask<ValueParseResult>(new ValueParseResult(true, 1986)));

            mockStringParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "whatev")))
                .Returns((ValueParserContext context) => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())));

            mockHandler_1
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (cntx, next) =>
                {
                    params_1 = cntx.Parameters;
                    return s_response;
                });

            mockHandler_2
                .Setup(h => h.Invoke(It.Is<RequestContext>(c => c.Request == _request), It.IsAny<CallNextHandlerDelegate>()))
                .Returns<RequestContext, CallNextHandlerDelegate>(async (cntx, next) =>
                {
                    params_2 = cntx.Parameters;
                    return await next();
                });

            InMemoryRouter router = _routerBuilder
                .AddValueParser("int", mockIntParser.Object)
                .AddValueParser("str", mockStringParser.Object)
                .AddPrefix("/api/users/*", bldr => bldr
                    .AddHandler("GET", "/{prefix:str}/{user_id:int}/dosomething/", mockHandler_1.Object)
                    .AddHandler("GET", "/{prefix:str}/{user_id_str:str}/dosomething/", mockHandler_2.Object))
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/api/users/whatev/1986/dosomething");

            Assert.That(await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            Assert.That(params_1, Is.EqualTo(new Dictionary<string, object> { ["prefix"] = "whatev", ["user_id"] = 1986 }));
            Assert.That(params_2, Is.Null);
            mockIntParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "1986")), Times.Once);
            mockStringParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "whatev")), Times.Once);
            mockStringParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "1986")), Times.Never);
        }

        [Test]
        public async Task Route_ShouldContinueMatchingWhenAValueParserReturnsFalse()
        {
            Mock<SyncValueParserDelegate>
                mockIntParser = new(MockBehavior.Strict),
                mockStringParser = new(MockBehavior.Strict);

            Mock<RequestHandlerDelegate>
                mockIntHandler = new(MockBehavior.Strict),
                mockStringHandler = new(MockBehavior.Strict);

            object?
                failedParse = null,
                successfulParse = "abc";

            mockIntParser
                .Setup(p => p.Invoke(It.Is<ReadOnlyMemory<char>>(segment => segment.ToString() == "abc"), It.IsAny<object?>(), out failedParse))
                .Returns(false);

            mockStringParser
                .Setup(p => p.Invoke(It.Is<ReadOnlyMemory<char>>(segment => segment.ToString() == "abc"), It.IsAny<object?>(), out successfulParse))
                .Returns(true);

            mockStringHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["slug"], "abc")),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            InMemoryRouter router = _routerBuilder
                .AddValueParser("int", mockIntParser.Object)
                .AddValueParser("str", mockStringParser.Object)
                .AddPrefix("/api/*", bldr => bldr
                    .AddHandler("GET", "/{id:int}/details/", mockIntHandler.Object)
                    .AddHandler("GET", "/{slug:str}/details/", mockStringHandler.Object))
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/api/abc/details");

            Assert.That(await router.Route(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockIntHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Never);
            mockStringHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
            mockIntParser.Verify(p => p.Invoke(It.Is<ReadOnlyMemory<char>>(segment => segment.ToString() == "abc"), It.IsAny<object?>(), out failedParse), Times.Once);
            mockStringParser.Verify(p => p.Invoke(It.Is<ReadOnlyMemory<char>>(segment => segment.ToString() == "abc"), It.IsAny<object?>(), out successfulParse), Times.Once);
        }
    }
}
