/********************************************************************************
* RouteMatchCursorTests.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
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

        private static RouteMatchCursor CreateCursor(RouteNode root, string path, MatchingBehavior matchingBehavior = MatchingBehavior.LiteralFirst) => new
        (
            root,
            HttpVerb.Get,
            new Uri($"https://www.example.com{path}", UriKind.Absolute),
            new Mock<IServiceProvider>(MockBehavior.Strict).Object,
            matchingBehavior,
            CancellationToken.None
        );

        [Test]
        public void ToString_ShouldExposeTheCurrentStack()
        {
            RouteNode root = new() { Segment = default };
            RouteMatchCursor cursor = CreateCursor(root, "/api/users");

            Assert.That(cursor.ToString(), Is.EqualTo("RouteMatchCursor { Stack = [0: { Phase = EmitHandlers, Segment = 'api', HandlerIndex = 0, ParsedChildIndex = 0 }] }"));
        }

        [Test]
        public async Task MoveNextAsync_ShouldMatchLiteralBranches()
        {
            HandlerRegistration handler = new(s_handler, "/api/users");

            RouteNode
                root = new() { Segment = default },
                api = new() { Segment = "api".AsMemory() },
                users = new() { Segment = "users".AsMemory() };

            users.HandlerRegistrations[HttpVerb.Get] = [handler];
            api.LiteralChildren.Add("users".AsMemory(), users);
            root.LiteralChildren.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/users");

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.Current.Handler, Is.EqualTo(handler.Handler));
            Assert.That(cursor.Current.Pattern, Is.EqualTo(handler.Pattern));
            Assert.That(cursor.Current.AttachedParameters, Is.Empty);

            Assert.That(await cursor.MoveNextAsync(), Is.False);
            Assert.That(cursor.ToString(), Is.EqualTo("RouteMatchCursor { Stack = [] }"));
        }

        [TestCase(MatchingBehavior.LiteralFirst, "/items/value")]
        [TestCase(MatchingBehavior.ParameterizedChildrenFirst, "/items/{id:str}")]
        public async Task MoveNextAsync_ShouldRespectMatchingBehavior(MatchingBehavior matchingBehavior, string expectedPattern)
        {
            HandlerRegistration
                literalHandler = new(s_handler, "/items/value"),
                parsedHandler = new(s_handler, "/items/{id:str}");

            RouteNode
                root = new() { Segment = default },
                items = new() { Segment = "items".AsMemory() },
                literal = new() { Segment = "value".AsMemory() },
                parsed = new()
                {
                    Segment = "{id:str}".AsMemory(),
                    SegmentParser = new SegmentParser
                    (
                        SegmentParserDefinition.Create("{id:str}"),
                        static context => new ValueTask<SegmentParseResult>(new SegmentParseResult(true, context.Segment.ToString())),
                        null
                    )
                };

            literal.HandlerRegistrations[HttpVerb.Get] = [literalHandler];
            parsed.HandlerRegistrations[HttpVerb.Get] = [parsedHandler];
            items.LiteralChildren.Add("value".AsMemory(), literal);
            items.ParsedChildren.Add(parsed);
            root.LiteralChildren.Add("items".AsMemory(), items);

            RouteMatchCursor cursor = CreateCursor(root, "/items/value", matchingBehavior);

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.Current.Pattern, Is.EqualTo(expectedPattern));

            if (matchingBehavior is MatchingBehavior.ParameterizedChildrenFirst)
                Assert.That(cursor.Current.AttachedParameters, Does.ContainKey("id").WithValue("value"));
        }

        [Test]
        public async Task MoveNextAsync_ShouldContinueWhenAParserReturnsFalse()
        {
            Mock<SegmentParserDelegate>
                mockIntParser = new(MockBehavior.Strict),
                mockStringParser = new(MockBehavior.Strict);

            MockSequence sequence = new();

            mockIntParser
                .InSequence(sequence)
                .Setup(parser => parser.Invoke(It.Is<SegmentParserContext>(context => context.Segment.ToString() == "abc")))
                .Returns((SegmentParserContext context) =>
                {
                    Assert.That(context.Arguments, Is.Null);
                    return new ValueTask<SegmentParseResult>(new SegmentParseResult(false, null));
                });

            mockStringParser
                .InSequence(sequence)
                .Setup(parser => parser.Invoke(It.Is<SegmentParserContext>(context => context.Segment.ToString() == "abc")))
                .Returns((SegmentParserContext context) =>
                {
                    Assert.That(context.Arguments, Is.Null);
                    return new ValueTask<SegmentParseResult>(new SegmentParseResult(true, context.Segment.ToString()));
                });

            HandlerRegistration handler = new(s_handler, "/api/{slug:str}/details");

            RouteNode
                root = new() { Segment = default },
                api = new() { Segment = "api".AsMemory() },
                intNode = new()
                {
                    Segment = "{id:int}".AsMemory(),
                    SegmentParser = new SegmentParser
                    (
                        SegmentParserDefinition.Create("{id:int}"),
                        mockIntParser.Object,
                        null
                    )
                },
                stringNode = new()
                {
                    Segment = "{slug:str}".AsMemory(),
                    SegmentParser = new SegmentParser
                    (
                        SegmentParserDefinition.Create("{slug:str}"),
                        mockStringParser.Object,
                        null
                    )
                },
                details = new() { Segment = "details".AsMemory() };

            details.HandlerRegistrations[HttpVerb.Get] = [handler];
            stringNode.LiteralChildren.Add("details".AsMemory(), details);
            api.ParsedChildren.Add(intNode);
            api.ParsedChildren.Add(stringNode);
            root.LiteralChildren.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/abc/details");

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.Current.Handler, Is.EqualTo(handler.Handler));
            Assert.That(cursor.Current.Pattern, Is.EqualTo(handler.Pattern));
            Assert.That(cursor.Current.AttachedParameters, Does.ContainKey("slug").WithValue("abc"));
            Assert.That(cursor.Current.AttachedParameters, Does.Not.ContainKey("id"));

            mockIntParser.Verify(parser => parser.Invoke(It.Is<SegmentParserContext>(context => context.Segment.ToString() == "abc")), Times.Once);
            mockStringParser.Verify(parser => parser.Invoke(It.Is<SegmentParserContext>(context => context.Segment.ToString() == "abc")), Times.Once);
        }

        [Test]
        public async Task MoveNextAsync_ShouldNotContinueWithSiblingParsedBranchesAfterABranchWasSelected()
        {
            Mock<SegmentParserDelegate>
                mockIntParser = new(MockBehavior.Strict),
                mockStringParser = new(MockBehavior.Strict);

            mockIntParser
                .Setup(parser => parser.Invoke(It.Is<SegmentParserContext>(context => context.Segment.ToString() == "1986")))
                .Returns(new ValueTask<SegmentParseResult>(new SegmentParseResult(true, 1986)));

            HandlerRegistration handler = new(s_handler, "/api/{id:int}/details");

            RouteNode
                root = new() { Segment = default },
                api = new() { Segment = "api".AsMemory() },
                intNode = new()
                {
                    Segment = "{id:int}".AsMemory(),
                    SegmentParser = new SegmentParser
                    (
                        SegmentParserDefinition.Create("{id:int}"),
                        mockIntParser.Object,
                        null
                    )
                },
                stringNode = new()
                {
                    Segment = "{slug:str}".AsMemory(),
                    SegmentParser = new SegmentParser
                    (
                        SegmentParserDefinition.Create("{slug:str}"),
                        mockStringParser.Object,
                        null
                    )
                },
                details = new() { Segment = "details".AsMemory() };

            details.HandlerRegistrations[HttpVerb.Get] = [handler];
            intNode.LiteralChildren.Add("details".AsMemory(), details);
            stringNode.LiteralChildren.Add("details".AsMemory(), new RouteNode { Segment = "details".AsMemory() });
            api.ParsedChildren.Add(intNode);
            api.ParsedChildren.Add(stringNode);
            root.LiteralChildren.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/1986/details");

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.Current.Pattern, Is.EqualTo(handler.Pattern));
            Assert.That(cursor.Current.AttachedParameters, Does.ContainKey("id").WithValue(1986));

            Assert.That(await cursor.MoveNextAsync(), Is.False);

            mockIntParser.Verify(parser => parser.Invoke(It.Is<SegmentParserContext>(context => context.Segment.ToString() == "1986")), Times.Once);
            mockStringParser.Verify(parser => parser.Invoke(It.IsAny<SegmentParserContext>()), Times.Never);
        }
    }
}
