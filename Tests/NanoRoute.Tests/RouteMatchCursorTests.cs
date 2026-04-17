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
            RouteNode root = new(default);
            RouteMatchCursor cursor = CreateCursor(root, "/api/users");

            Assert.That(cursor.ToString(), Is.EqualTo("RouteMatchCursor { Stack = [0: { Phase = EmitHandlers, Segment = 'api', HandlerIndex = 0, ParsedChildIndex = 0 }] }"));
        }

        [Test]
        public async Task ToString_ShouldSeparateMultipleFrames()
        {
            RouteNode
                root = new(default),
                api = new("api".AsMemory()),
                users = new("users".AsMemory());

            users.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(s_handler, "/api/users")];
            api.LiteralChildren.Add("users".AsMemory(), users);
            root.LiteralChildren.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/users");

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.ToString(), Is.EqualTo("RouteMatchCursor { Stack = [0: { Phase = SecondBranch, Segment = 'api', HandlerIndex = 0, ParsedChildIndex = 0 }, 1: { Phase = SecondBranch, Segment = 'users', HandlerIndex = 0, ParsedChildIndex = 0 }, 2: { Phase = EmitHandlers, Segment = '', HandlerIndex = 1, ParsedChildIndex = 0 }] }"));
        }

        [Test]
        public async Task MoveNextAsync_ShouldMatchLiteralBranches()
        {
            HandlerRegistration handler = new(s_handler, "/api/users");

            RouteNode
                root = new(default),
                api = new("api".AsMemory()),
                users = new("users".AsMemory());

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
                root = new(default),
                items = new("items".AsMemory()),
                literal = new("value".AsMemory()),
                parsed = new("{id:str}".AsMemory())
                {
                    ParameterParser = new ParameterParser
                    (
                        ParameterDefinition.Create("{id:str}"),
                        static context => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())),
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
            Mock<ValueParserDelegate>
                mockIntParser = new(MockBehavior.Strict),
                mockStringParser = new(MockBehavior.Strict);

            MockSequence sequence = new();

            mockIntParser
                .InSequence(sequence)
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "abc")))
                .Returns((ValueParserContext context) =>
                {
                    Assert.That(context.Arguments, Is.Null);
                    return new ValueTask<ValueParseResult>(new ValueParseResult(false, null));
                });

            mockStringParser
                .InSequence(sequence)
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "abc")))
                .Returns((ValueParserContext context) =>
                {
                    Assert.That(context.Arguments, Is.Null);
                    return new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString()));
                });

            HandlerRegistration handler = new(s_handler, "/api/{slug:str}/details");

            RouteNode
                root = new(default),
                api = new("api".AsMemory()),
                intNode = new("{id:int}".AsMemory())
                {
                    ParameterParser = new ParameterParser
                    (
                        ParameterDefinition.Create("{id:int}"),
                        mockIntParser.Object,
                        null
                    )
                },
                stringNode = new("{slug:str}".AsMemory())
                {
                    ParameterParser = new ParameterParser
                    (
                        ParameterDefinition.Create("{slug:str}"),
                        mockStringParser.Object,
                        null
                    )
                },
                details = new("details".AsMemory());

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

            mockIntParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "abc")), Times.Once);
            mockStringParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "abc")), Times.Once);
        }

        [Test]
        public async Task MoveNextAsync_ShouldNotAttachParametersWhenTheParsedSegmentHasNoParameterName()
        {
            HandlerRegistration handler = new(s_handler, "/api/{str}/details");

            RouteNode
                root = new(default),
                api = new("api".AsMemory()),
                parsed = new("{str}".AsMemory())
                {
                    ParameterParser = new ParameterParser
                    (
                        ParameterDefinition.Create("{str}"),
                        static context => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())),
                        null
                    )
                },
                details = new("details".AsMemory());

            details.HandlerRegistrations[HttpVerb.Get] = [handler];
            parsed.LiteralChildren.Add("details".AsMemory(), details);
            api.ParsedChildren.Add(parsed);
            root.LiteralChildren.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/value/details");

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.Current.Pattern, Is.EqualTo(handler.Pattern));
            Assert.That(cursor.Current.AttachedParameters, Is.Empty);
        }

        [Test]
        public async Task MoveNextAsync_ShouldNotContinueWithSiblingParsedBranchesAfterABranchWasSelected()
        {
            Mock<ValueParserDelegate>
                mockIntParser = new(MockBehavior.Strict),
                mockStringParser = new(MockBehavior.Strict);

            mockIntParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "1986")))
                .Returns(new ValueTask<ValueParseResult>(new ValueParseResult(true, 1986)));

            HandlerRegistration handler = new(s_handler, "/api/{id:int}/details");

            RouteNode
                root = new(default),
                api = new("api".AsMemory()),
                intNode = new("{id:int}".AsMemory())
                {
                    ParameterParser = new ParameterParser
                    (
                        ParameterDefinition.Create("{id:int}"),
                        mockIntParser.Object,
                        null
                    )
                },
                stringNode = new("{slug:str}".AsMemory())
                {
                    ParameterParser = new ParameterParser
                    (
                        ParameterDefinition.Create("{slug:str}"),
                        mockStringParser.Object,
                        null
                    )
                },
                details = new("details".AsMemory());

            details.HandlerRegistrations[HttpVerb.Get] = [handler];
            intNode.LiteralChildren.Add("details".AsMemory(), details);
            stringNode.LiteralChildren.Add("details".AsMemory(), new RouteNode("details".AsMemory()));
            api.ParsedChildren.Add(intNode);
            api.ParsedChildren.Add(stringNode);
            root.LiteralChildren.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/1986/details");

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.Current.Pattern, Is.EqualTo(handler.Pattern));
            Assert.That(cursor.Current.AttachedParameters, Does.ContainKey("id").WithValue(1986));

            Assert.That(await cursor.MoveNextAsync(), Is.False);

            mockIntParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "1986")), Times.Once);
            mockStringParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Never);
        }

        [Test]
        public void Ctor_ShouldThrowOnUnknownMatchingBehavior()
        {
            RouteNode root = new(default);

            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => CreateCursor(root, "/api", (MatchingBehavior) 99))!;

            Assert.That(ex.ParamName, Is.EqualTo("matchingBehavior"));
        }
    }
}

