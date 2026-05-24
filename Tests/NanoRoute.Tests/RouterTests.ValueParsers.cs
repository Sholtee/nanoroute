/********************************************************************************
* RouterTests.ValueParsers.cs                                                   *
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
    internal sealed partial class RouterTests
    {
        [Test]
        public async Task Handle_ShouldSupportParsedSegmentsWithoutBindingTheirValueToParameters()
        {
            Mock<SyncValueParserDelegate> mockParser = new(MockBehavior.Strict);
            Dictionary<string, object?> paramz = null!;
            object? parsed = "any_string";

            mockParser
                .Setup(p => p.Invoke(It.Is<ReadOnlyMemory<char>>(segment => segment.ToString() == "any_string"), It.IsAny<object?>(), out parsed))
                .Returns(true);

            TestRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddValueParser("slug", mockParser.Object)
                .AddHandler("GET", "/users/{user_id:int}/{slug}/cica/", async (context, next) =>
                {
                    paramz = context.Parameters;
                    return s_response;
                })
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/users/1986/any_string/cica");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockParser.Verify(p => p.Invoke(It.Is<ReadOnlyMemory<char>>(segment => segment.ToString() == "any_string"), It.IsAny<object?>(), out parsed), Times.Once);
            Assert.That(paramz, Has.Count.EqualTo(1));
            Assert.That(paramz, Does.ContainKey("user_id").WithValue(1986));
            Assert.That(paramz, Does.Not.ContainKey("slug"));
        }

        [Test]
        public async Task Handle_ShouldPassDecodedSegmentsToSyncValueParsers()
        {
            Mock<SyncValueParserDelegate> mockParser = new(MockBehavior.Strict);
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);

            object? parsed = "a b";
            mockParser
                .Setup(p => p.Invoke(It.Is<ReadOnlyMemory<char>>(segment => segment.ToString() == "a b"), It.IsAny<object?>(), out parsed))
                .Returns(true);

            mockHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["name"], "a b")),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddValueParser("str", mockParser.Object)
                .AddHandler("GET", "/files/{name:str}/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/files/a%20b");

            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));
            mockParser.Verify(p => p.Invoke(It.Is<ReadOnlyMemory<char>>(segment => segment.ToString() == "a b"), It.IsAny<object?>(), out parsed), Times.Once);
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldPassDecodedSegmentsToAsyncValueParsers()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            Mock<IServiceProvider> mockServices = new(MockBehavior.Strict);

            mockParser
                .Setup(p => p.Invoke(It.Is<ValueParserContext>(ctx => ctx.Segment.ToString() == "a b" && ctx.Services == mockServices.Object)))
                .Returns((ValueParserContext context) =>
                {
                    return new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString()));
                });

            mockHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["name"], "a b")),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddValueParser("str", mockParser.Object)
                .AddHandler("GET", "/files/{name:str}/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/files/a%20b");

            Assert.That(await router.Handle(_request, mockServices.Object), Is.EqualTo(s_response));
            mockParser.Verify(p => p.Invoke(It.Is<ValueParserContext>(ctx => ctx.Segment.ToString() == "a b" && ctx.Services == mockServices.Object)), Times.Once);
            mockHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Handle_ShouldAcceptParsersWithAndWithoutStateMachine(bool forceStateMachine)
        {
            static ValueTask<ValueParseResult> ParserWithoutStateMachine(ValueParserContext context) =>
                new(new ValueParseResult(true, context.Segment.ToString()));

            static async ValueTask<ValueParseResult> ParserWithStateMachine(ValueParserContext context)
            {
                await Task.Yield();
                return new ValueParseResult(true, context.Segment.ToString());
            }

            TestRouter router = _routerBuilder
                .AddValueParser("str", forceStateMachine ? ParserWithStateMachine : ParserWithoutStateMachine)
                .AddHandler("GET", "/files/{name:str}/", async (context, _) => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent((string) context.Parameters["name"]!)
                })
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/files/a%20b");

            HttpResponseMessage response = await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("a b"));
        }

        [Test]
        public async Task Handle_ShouldAwaitPendingValueParser()
        {
            TaskCompletionSource<ValueParseResult> parserResult = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            Mock<RequestHandlerDelegate> mockHandler = new(MockBehavior.Strict);
            Mock<IServiceProvider> mockServices = new(MockBehavior.Loose);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "delayed" && context.Services == mockServices.Object)))
                .Returns((ValueParserContext _) => new ValueTask<ValueParseResult>(parserResult.Task));

            mockHandler
                .Setup(handler => handler.Invoke
                (
                    It.Is<RequestContext>(context => context.Request == _request && Equals(context.Parameters["name"], "delayed")),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddValueParser("str", mockParser.Object)
                .AddHandler("GET", "/files/{name:str}/", mockHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/files/delayed");

            Task<HttpResponseMessage> response = router.Handle(_request, mockServices.Object);
            Assert.That(response.IsCompleted, Is.False);

            mockParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "delayed" && context.Services == mockServices.Object)), Times.Once);
            mockHandler.Verify(handler => handler.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Never);

            parserResult.SetResult(new ValueParseResult(true, "delayed"));

            Assert.That(await response, Is.EqualTo(s_response));

            mockHandler.Verify(handler => handler.Invoke(It.Is<RequestContext>(context => context.Request == _request && Equals(context.Parameters["name"], "delayed")), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task Handle_ShouldPassBoundParserArgumentsToValueParsers()
        {
            (int Min, string Text) boundArguments = (3, "it's okay");

            Mock<BindArgumentsDelegate> mockBindArguments = new(MockBehavior.Strict);
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);

            mockBindArguments
                .Setup(b => b.Invoke(It.Is<IReadOnlyDictionary<string, string>>(args =>
                    args.Count == 2 &&
                    args["min"] == "3" &&
                    args["text"] == "it's okay")))
                .Returns(boundArguments);

            mockParser
                .Setup(p => p.Invoke(It.Is<ValueParserContext>(ctx => ctx.Segment.ToString() == "abcd" && Equals(ctx.Arguments, boundArguments))))
                .Returns((ValueParserContext ctx) => new ValueTask<ValueParseResult>(new ValueParseResult(true, ctx.Segment.ToString())));

            TestRouter router = _routerBuilder
                .AddValueParser
                (
                    "bounded",
                    mockBindArguments.Object,
                    mockParser.Object
                )
                .AddHandler("GET", "/files/{name:bounded(min=3,text='it\\'s okay')}/", async (context, _) => new HttpResponseMessage { Content = new StringContent((string) context.Parameters["name"]!) })
                .CreateRouter();

            HttpRequestMessage request = new() { Method = HttpMethod.Get, RequestUri = new Uri("https://www.exmaple.com/files/abcd") };

            HttpResponseMessage
                response1 = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object),
                response2 = await router.Handle(request, new Mock<IServiceProvider>(MockBehavior.Loose).Object);

            Assert.That(await response1.Content.ReadAsStringAsync(), Is.EqualTo("abcd"));
            Assert.That(await response2.Content.ReadAsStringAsync(), Is.EqualTo("abcd"));
            mockBindArguments.Verify(b => b.Invoke(It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Once);
            mockParser.Verify(p => p.Invoke(It.IsAny<ValueParserContext>()), Times.Exactly(2));
        }

        [Test]
        public async Task Handle_ShouldPassBoundParserArgumentsToSynchronousValueParsers()
        {
            (int Min, string Text) boundArguments = (3, "it's okay");

            Mock<BindArgumentsDelegate> mockBindArguments = new(MockBehavior.Strict);
            Mock<SyncValueParserDelegate> mockParser = new(MockBehavior.Strict);
            object? parsed = "abcd";

            mockBindArguments
                .Setup(b => b.Invoke(It.Is<IReadOnlyDictionary<string, string>>(args =>
                    args.Count == 2 &&
                    args["min"] == "3" &&
                    args["text"] == "it's okay")))
                .Returns(boundArguments);

            mockParser
                .Setup(p => p.Invoke
                (
                    It.Is<ReadOnlyMemory<char>>(segment => segment.ToString() == "abcd"),
                    It.Is<object?>(args => Equals(args, boundArguments)),
                    out parsed
                ))
                .Returns(true);

            TestRouter router = _routerBuilder
                .AddValueParser
                (
                    "bounded",
                    mockBindArguments.Object,
                    mockParser.Object
                )
                .AddHandler("GET", "/files/{name:bounded(min=3,text='it\\'s okay')}/", async (context, _) => new HttpResponseMessage { Content = new StringContent((string) context.Parameters["name"]!) })
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/files/abcd");

            HttpResponseMessage
                response1 = await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object),
                response2 = await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object);

            Assert.That(await response1.Content.ReadAsStringAsync(), Is.EqualTo("abcd"));
            Assert.That(await response2.Content.ReadAsStringAsync(), Is.EqualTo("abcd"));
            mockBindArguments.Verify(b => b.Invoke(It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Once);
            mockParser.Verify(p => p.Invoke
            (
                It.Is<ReadOnlyMemory<char>>(segment => segment.ToString() == "abcd"),
                It.Is<object?>(args => Equals(args, boundArguments)),
                out parsed
            ), Times.Exactly(2));
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
                .AddHandler("GET", "/items/{value:int(min=10,max=20)}/", boundedHandler.Object)
                .AddHandler("GET", "/items/{value:int}/", fallbackHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/items/15");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));

            _request.RequestUri = new Uri("https://www.exmaple.com/items/25");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));

            boundedHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
            fallbackHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task AddStringParser_ShouldRespectMinAndMaxParameters()
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
                .AddHandler("GET", "/tags/{slug:str(min=3,max=3)}/", constrainedHandler.Object)
                .AddHandler("GET", "/tags/{slug:str}/", fallbackHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/tags/abc");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));

            _request.RequestUri = new Uri("https://www.exmaple.com/tags/abcd");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));

            constrainedHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
            fallbackHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task AddRegexParser_ShouldDefaultToCaseInsensitiveMatchingAndRespectCaseSensitiveParameter()
        {
            Mock<RequestHandlerDelegate>
                constrainedHandler = new(MockBehavior.Strict),
                fallbackHandler = new(MockBehavior.Strict),
                caseInsensitiveHandler = new(MockBehavior.Strict);

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
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["slug"], "ABC")),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            caseInsensitiveHandler
                .Setup(h => h.Invoke
                (
                    It.Is<RequestContext>(c => c.Request == _request && Equals(c.Parameters["slug"], "ABC")),
                    It.IsAny<CallNextHandlerDelegate>()
                ))
                .ReturnsAsync(s_response);

            TestRouter router = _routerBuilder
                .AddRegexParser()
                .AddStringParser()
                .AddHandler("GET", "/tags/{slug:regex(pattern='^[a-z]+$',caseSensitive=true)}/", constrainedHandler.Object)
                .AddHandler("GET", "/tags/{slug:str}/", fallbackHandler.Object)
                .AddHandler("GET", "/labels/{slug:regex(pattern='^[a-z]+$')}/", caseInsensitiveHandler.Object)
                .CreateRouter();

            _request.RequestUri = new Uri("https://www.exmaple.com/tags/abc");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));

            _request.RequestUri = new Uri("https://www.exmaple.com/tags/ABC");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));

            _request.RequestUri = new Uri("https://www.exmaple.com/labels/ABC");
            Assert.That(await router.Handle(_request, new Mock<IServiceProvider>(MockBehavior.Loose).Object), Is.EqualTo(s_response));

            constrainedHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
            fallbackHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
            caseInsensitiveHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<CallNextHandlerDelegate>()), Times.Once);
        }

        [Test]
        public async Task AddRegexParser_ShouldTreatTimedOutMatchesAsNonMatches()
        {
            _routerBuilder.AddRegexParser();

            ValueParserRegistration parser = _routerBuilder.ValueParsers["regex"];

            object? parserArguments = parser.BindArguments(new Dictionary<string, string>
            {
                // The nested quantified pattern catastrophically backtracks when a long run of 'a'
                // characters is followed by a non-matching tail. The 1 ms timeout forces Regex to
                // throw RegexMatchTimeoutException, which the parser converts into a non-match.
                ["pattern"] = "^(a+)+$",
                ["timeoutMs"] = "1"
            });

            ValueParseResult result = await parser.Parse(new ValueParserContext
            {
                Segment = $"{new string('a', 20)}!".AsMemory(),
                Services = new Mock<IServiceProvider>(MockBehavior.Loose).Object,
                Arguments = parserArguments
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Parsed, Is.Null);
        }

        [TestCase("/items/{value:int}/")]
        [TestCase("/items/{value:int()}/")]
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

        [TestCase("/items/{value:guid}/")]
        [TestCase("/items/{value:guid()}/")]
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

        [TestCase("/flags/{value:bool}/")]
        [TestCase("/flags/{value:bool()}/")]
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

        [TestCase("/tags/{value:str}/")]
        [TestCase("/tags/{value:str()}/")]
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
    }
}
