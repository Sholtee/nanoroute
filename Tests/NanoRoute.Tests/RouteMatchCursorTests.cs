/********************************************************************************
* RouteMatchCursorTests.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class RouteMatchCursorTests
    {
        private static readonly RequestHandlerDelegate s_handler = static (_, _) => Task.FromResult(new HttpResponseMessage());

        private static ParameterDefinition Parse(string segment)
        {
            int offset = 0;
            ParameterDefinition definition = ParameterDefinition.Parse(segment, ref offset);

            Assert.That(offset, Is.EqualTo(segment.Length - 1));

            return definition;
        }

        private static KeyValuePair<ParameterParser, RouteNode> ParsedBranch(string segment, ValueParserDelegate parser, RouteNode node, object? arguments = null) => new
        (
            new ParameterParser(Parse(segment), parser, arguments),
            node
        );

        private static void AddHandler(RouteNode node, HttpVerb verb, HandlerRegistration handler) =>
            node.Handlers.Add(new KeyValuePair<HttpVerb, HandlerRegistration>(verb, handler));

        private static RouteMatchCursor CreateCursor(RouteNode root, string path, MatchingPrecedence matchingPrecedence = MatchingPrecedence.LiteralFirst) => new
        (
            root.Freeze(),
            HttpVerb.Get,
            new Uri($"https://www.example.com{path}", UriKind.Absolute),
            new Mock<IServiceProvider>(MockBehavior.Strict).Object,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            matchingPrecedence,
            CancellationToken.None
        );

        [Test]
        public async Task MoveNextAsync_ShouldMatchLiteralBranches()
        {
            HandlerRegistration handler = new(s_handler, "/api/users/");

            RouteNode
                root = new(),
                api = new(),
                users = new();

            AddHandler(users, HttpVerb.Get, handler);
            api.LiteralBranches.Add("users".AsMemory(), users);
            root.LiteralBranches.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/users");
            Assert.That(cursor.Completed, Is.False);

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.HandlerRegistration, Is.EqualTo(handler));
            Assert.That(cursor.RemainingPath.ToString(), Is.Empty);
            Assert.That(cursor.Parameters, Is.Empty);

            Assert.That(await cursor.MoveNextAsync(), Is.False);
            Assert.That(cursor.Completed);
        }

        [Test]
        public async Task MoveNextAsync_ShouldMatchPrefixBranches()
        {
            HandlerRegistration handler = new(s_handler, "/api/*");

            RouteNode
                root = new(),
                api = new();

            AddHandler(api, HttpVerb.Get, handler);
            root.LiteralBranches.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/health");
            Assert.That(cursor.Completed, Is.False);

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.HandlerRegistration, Is.EqualTo(handler));
            Assert.That(cursor.RemainingPath.ToString(), Is.EqualTo("/health"));
            Assert.That(cursor.Parameters, Is.Empty);

            Assert.That(await cursor.MoveNextAsync(), Is.False);
            Assert.That(cursor.Completed);
        }

        [Test]
        public async Task MoveNextAsync_ShouldReportRemainingPathForRootPrefix()
        {
            HandlerRegistration handler = new(s_handler, "/*");

            RouteNode root = new();
            AddHandler(root, HttpVerb.Get, handler);

            RouteMatchCursor cursor = CreateCursor(root, "/api/health");
            Assert.That(cursor.Completed, Is.False);

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.HandlerRegistration, Is.EqualTo(handler));
            Assert.That(cursor.RemainingPath.ToString(), Is.EqualTo("/api/health"));
            Assert.That(cursor.Parameters, Is.Empty);

            Assert.That(await cursor.MoveNextAsync(), Is.False);
            Assert.That(cursor.Completed);
        }

        [TestCase(MatchingPrecedence.LiteralFirst, "/items/value/")]
        [TestCase(MatchingPrecedence.ParameterizedFirst, "/items/{id:str}/")]
        public async Task MoveNextAsync_ShouldRespectMatchingPrecedence(MatchingPrecedence matchingPrecedence, string expectedPattern)
        {
            HandlerRegistration
                literalHandler = new(s_handler, "/items/value/"),
                parsedHandler = new(s_handler, "/items/{id:str}/");

            RouteNode
                root = new(),
                items = new(),
                literal = new(),
                parsed = new();

            AddHandler(literal, HttpVerb.Get, literalHandler);
            AddHandler(parsed, HttpVerb.Get, parsedHandler);
            items.LiteralBranches.Add("value".AsMemory(), literal);
            items.ParsedBranches.Add(ParsedBranch("{id:str}", static async context => new ValueParseResult(true, context.Segment.ToString()), parsed));
            root.LiteralBranches.Add("items".AsMemory(), items);

            RouteMatchCursor cursor = CreateCursor(root, "/items/value", matchingPrecedence);

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.HandlerRegistration.Pattern, Is.EqualTo(expectedPattern));

            if (matchingPrecedence is MatchingPrecedence.ParameterizedFirst)
                Assert.That(cursor.Parameters, Does.ContainKey("id").WithValue("value"));

            Assert.That(await cursor.MoveNextAsync(), Is.False);
        }

        [Test]
        public async Task MoveNextAsync_ShouldContinueWhenAParserReturnsFalse()
        {
            Mock<ValueParserDelegate>
                mockIntParser = new(MockBehavior.Strict),
                mockStringParser = new(MockBehavior.Strict);

            MockSequence sequence = new();

            mockIntParser
                .InSequence(sequence)
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "abc")))
                .Returns(async (ValueParserContext context) =>
                {
                    Assert.That(context.Arguments, Is.Null);
                    return ValueParseResult.False;
                });

            mockStringParser
                .InSequence(sequence)
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "abc")))
                .Returns(async (ValueParserContext context) =>
                {
                    Assert.That(context.Arguments, Is.Null);
                    return new ValueParseResult(true, context.Segment.ToString());
                });

            HandlerRegistration handler = new(s_handler, "/api/{slug:str}/details/");

            RouteNode
                root = new(),
                api = new(),
                intNode = new(),
                stringNode = new(),
                details = new();

            AddHandler(details, HttpVerb.Get, handler);
            stringNode.LiteralBranches.Add("details".AsMemory(), details);
            api.ParsedBranches.Add(ParsedBranch("{id:int}", mockIntParser.Object, intNode));
            api.ParsedBranches.Add(ParsedBranch("{slug:str}", mockStringParser.Object, stringNode));
            root.LiteralBranches.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/abc/details");

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.HandlerRegistration, Is.EqualTo(handler));
            Assert.That(cursor.Parameters, Does.ContainKey("slug").WithValue("abc"));
            Assert.That(cursor.Parameters, Does.Not.ContainKey("id"));

            mockIntParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "abc")), Times.Once);
            mockStringParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "abc")), Times.Once);
        }

        [Test]
        public async Task MoveNextAsync_ShouldNotAttachParametersWhenTheParsedSegmentHasNoParameterName()
        {
            HandlerRegistration handler = new(s_handler, "/api/{str}/details/");

            RouteNode
                root = new(),
                api = new(),
                parsed = new(),
                details = new();

            AddHandler(details, HttpVerb.Get, handler);
            parsed.LiteralBranches.Add("details".AsMemory(), details);
            api.ParsedBranches.Add(ParsedBranch("{str}", static async context => new ValueParseResult(true, context.Segment.ToString()), parsed));
            root.LiteralBranches.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/value/details");

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.HandlerRegistration.Pattern, Is.EqualTo(handler.Pattern));
            Assert.That(cursor.Parameters, Is.Empty);
        }

        [Test]
        public async Task MoveNextAsync_ShouldNotContinueWithSiblingParsedBranchesAfterABranchWasSelected()
        {
            Mock<ValueParserDelegate>
                mockIntParser = new(MockBehavior.Strict),
                mockStringParser = new(MockBehavior.Strict);

            mockIntParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "1986")))
                .ReturnsAsync(new ValueParseResult(true, 1986));

            HandlerRegistration handler = new(s_handler, "/api/{id:int}/details/");

            RouteNode
                root = new(),
                api = new(),
                intNode = new(),
                stringNode = new(),
                details = new();

            AddHandler(details, HttpVerb.Get, handler);
            intNode.LiteralBranches.Add("details".AsMemory(), details);
            stringNode.LiteralBranches.Add("details".AsMemory(), new RouteNode());
            api.ParsedBranches.Add(ParsedBranch("{id:int}", mockIntParser.Object, intNode));
            api.ParsedBranches.Add(ParsedBranch("{slug:str}", mockStringParser.Object, stringNode));
            root.LiteralBranches.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/1986/details");

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.HandlerRegistration.Pattern, Is.EqualTo(handler.Pattern));
            Assert.That(cursor.Parameters, Does.ContainKey("id").WithValue(1986));

            Assert.That(await cursor.MoveNextAsync(), Is.False);

            mockIntParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "1986")), Times.Once);
            mockStringParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Never);
        }

        [Test]
        public async Task MoveNextAsync_ShouldAwaitParameterizedBranchBeforeLiteralFallback()
        {
            TaskCompletionSource<ValueParseResult> parserResult = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "literal")))
                .Returns((ValueParserContext _) => new ValueTask<ValueParseResult>(parserResult.Task));

            HandlerRegistration handler = new(s_handler, "/items/literal/");

            RouteNode
                root = new(),
                items = new(),
                literal = new(),
                parsed = new();

            AddHandler(literal, HttpVerb.Get, handler);
            items.LiteralBranches.Add("literal".AsMemory(), literal);
            items.ParsedBranches.Add(ParsedBranch("{id:str}", mockParser.Object, parsed));
            root.LiteralBranches.Add("items".AsMemory(), items);

            RouteMatchCursor cursor = CreateCursor(root, "/items/literal", MatchingPrecedence.ParameterizedFirst);

            ValueTask<bool> pendingMove = cursor.MoveNextAsync();
            Assert.That(pendingMove.IsCompleted, Is.False);

            parserResult.SetResult(ValueParseResult.False);

            Assert.That(await pendingMove, Is.True);
            Assert.That(cursor.HandlerRegistration, Is.EqualTo(handler));
            Assert.That(await cursor.MoveNextAsync(), Is.False);

            mockParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "literal")), Times.Once);
        }

        [Test]
        public async Task MoveNextAsync_ShouldAwaitParameterizedBranchAndSkipLiteralFallbackWhenItMatches()
        {
            TaskCompletionSource<ValueParseResult> parserResult = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "literal")))
                .Returns((ValueParserContext _) => new ValueTask<ValueParseResult>(parserResult.Task));

            HandlerRegistration
                literalHandler = new(s_handler, "/items/literal/"),
                parsedHandler = new(s_handler, "/items/{id:str}/");

            RouteNode
                root = new(),
                items = new(),
                literal = new(),
                parsed = new();

            AddHandler(literal, HttpVerb.Get, literalHandler);
            AddHandler(parsed, HttpVerb.Get, parsedHandler);
            items.LiteralBranches.Add("literal".AsMemory(), literal);
            items.ParsedBranches.Add(ParsedBranch("{id:str}", mockParser.Object, parsed));
            root.LiteralBranches.Add("items".AsMemory(), items);

            RouteMatchCursor cursor = CreateCursor(root, "/items/literal", MatchingPrecedence.ParameterizedFirst);

            ValueTask<bool> pendingMove = cursor.MoveNextAsync();
            Assert.That(pendingMove.IsCompleted, Is.False);

            parserResult.SetResult(new ValueParseResult(true, "literal"));

            Assert.That(await pendingMove, Is.True);
            Assert.That(cursor.HandlerRegistration, Is.EqualTo(parsedHandler));
            Assert.That(cursor.Parameters, Does.ContainKey("id").WithValue("literal"));
            Assert.That(await cursor.MoveNextAsync(), Is.False);

            mockParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "literal")), Times.Once);
        }

        [Test]
        public async Task MoveNextAsync_ShouldAwaitParameterizedBranchBeforeCompletingWithoutFallback()
        {
            TaskCompletionSource<ValueParseResult> parserResult = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "missing")))
                .Returns((ValueParserContext _) => new ValueTask<ValueParseResult>(parserResult.Task));

            RouteNode
                root = new(),
                items = new(),
                parsed = new();

            items.ParsedBranches.Add(ParsedBranch("{id:str}", mockParser.Object, parsed));
            root.LiteralBranches.Add("items".AsMemory(), items);

            RouteMatchCursor cursor = CreateCursor(root, "/items/missing", MatchingPrecedence.ParameterizedFirst);

            ValueTask<bool> pendingMove = cursor.MoveNextAsync();
            Assert.That(pendingMove.IsCompleted, Is.False);

            parserResult.SetResult(ValueParseResult.False);

            Assert.That(await pendingMove, Is.False);
            Assert.That(cursor.Completed, Is.True);

            mockParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "missing")), Times.Once);
        }

        [Test]
        public async Task MoveNextAsync_ShouldAwaitSingleParameterizedBranchAndContinueWithNextSingleBranch()
        {
            TaskCompletionSource<ValueParseResult> parserResult = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "value")))
                .Returns((ValueParserContext _) => new ValueTask<ValueParseResult>(parserResult.Task));

            HandlerRegistration handler = new(s_handler, "/{id:str}/details/");

            RouteNode
                root = new(),
                parsed = new(),
                details = new();

            AddHandler(details, HttpVerb.Get, handler);
            parsed.LiteralBranches.Add("details".AsMemory(), details);
            root.ParsedBranches.Add(ParsedBranch("{id:str}", mockParser.Object, parsed));

            RouteMatchCursor cursor = CreateCursor(root, "/value/details");

            ValueTask<bool> pendingMove = cursor.MoveNextAsync();
            Assert.That(pendingMove.IsCompleted, Is.False);

            parserResult.SetResult(new ValueParseResult(true, "value"));

            Assert.That(await pendingMove, Is.True);
            Assert.That(cursor.HandlerRegistration, Is.EqualTo(handler));
            Assert.That(cursor.Parameters, Does.ContainKey("id").WithValue("value"));
            Assert.That(await cursor.MoveNextAsync(), Is.False);

            mockParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "value")), Times.Once);
        }

        [Test]
        public async Task MoveNextAsync_ShouldCompleteWhenSingleParameterizedBranchDoesNotMatch()
        {
            RouteNode root = new();
            root.ParsedBranches.Add(ParsedBranch("{id:str}", static _ => new ValueTask<ValueParseResult>(ValueParseResult.False), new RouteNode()));

            RouteMatchCursor cursor = CreateCursor(root, "/value");

            Assert.That(await cursor.MoveNextAsync(), Is.False);
            Assert.That(cursor.Completed, Is.True);
        }

        [Test]
        public async Task MoveNextAsync_ShouldCompleteWhenSingleLiteralBranchDoesNotMatch()
        {
            RouteNode root = new();
            root.LiteralBranches.Add("cica".AsMemory(), new RouteNode());

            RouteMatchCursor cursor = CreateCursor(root, "/value");

            Assert.That(await cursor.MoveNextAsync(), Is.False);
            Assert.That(cursor.Completed, Is.True);
        }

        [Test]
        public async Task Reset_ShouldRestartMatchingAfterDecoding()
        {
            HandlerRegistration handler = new(s_handler, "/files/a%20b/");

            RouteNode
                root = new(),
                files = new(),
                name = new();

            AddHandler(name, HttpVerb.Get, handler);
            files.LiteralBranches.Add("a b".AsMemory(), name);
            root.LiteralBranches.Add("files".AsMemory(), files);

            using RouteMatchCursor cursor = CreateCursor(root, "/files/a%20b");

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.HandlerRegistration, Is.EqualTo(handler));
            Assert.That(await cursor.MoveNextAsync(), Is.False);
            Assert.That(cursor.Completed, Is.True);

            cursor.Reset();

            Assert.That(cursor.Completed, Is.False);
            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.HandlerRegistration, Is.EqualTo(handler));
            Assert.That(await cursor.MoveNextAsync(), Is.False);
            Assert.That(cursor.Completed, Is.True);
        }

        [Test]
        public async Task MoveNextAsync_ShouldRejectInvalidPathEscapes()
        {
            RouteNode
                root = new(),
                files = new(),
                parsed = new();

            files.ParsedBranches.Add(ParsedBranch("{name:str}", static context => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())), parsed));
            root.LiteralBranches.Add("files".AsMemory(), files);

            using RouteMatchCursor cursor = CreateCursor(root, "/files/%C0%AF");

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(async () => await cursor.MoveNextAsync())!;
            Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.BadRequest));
        }
    }
}
