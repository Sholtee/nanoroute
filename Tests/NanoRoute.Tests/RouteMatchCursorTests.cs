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

    [TestFixture(true)]
    [TestFixture(false)]
    internal sealed class RouteMatchCursorTests(bool freezeRoot)
    {
        private static readonly RequestHandlerDelegate s_handler = static (_, _) => Task.FromResult(new HttpResponseMessage());

        private static ParameterDefinition Parse(string segment)
        {
            int offset = 0;
            ParameterDefinition definition = ParameterDefinition.Parse(segment, ref offset);

            Assert.That(offset, Is.EqualTo(segment.Length - 1));

            return definition;
        }

        private RouteMatchCursor CreateCursor(RouteNode root, string path, MatchingPrecedence matchingPrecedence = MatchingPrecedence.LiteralFirst) => new
        (
            root.Copy(freezeRoot),
            HttpVerb.Get,
            new Uri($"https://www.example.com{path}", UriKind.Absolute),
            new Mock<IServiceProvider>(MockBehavior.Strict).Object,
            new RouterConfig { MatchingPrecedence = matchingPrecedence },
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

            users.HandlerRegistrations[HttpVerb.Get] = [handler];
            api.LiteralChildren.Add("users".AsMemory(), users);
            root.LiteralChildren.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/users");
            Assert.That(cursor.Completed, Is.False);

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.Current.HandlerRegistration, Is.EqualTo(handler));
            Assert.That(cursor.Current.RemainingPath.ToString(), Is.Empty);
            Assert.That(cursor.Current.AttachedParameters, Is.Empty);

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

            api.HandlerRegistrations[HttpVerb.Get] = [handler];
            root.LiteralChildren.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/health");
            Assert.That(cursor.Completed, Is.False);

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.Current.HandlerRegistration, Is.EqualTo(handler));
            Assert.That(cursor.Current.RemainingPath.ToString(), Is.EqualTo("/health"));
            Assert.That(cursor.Current.AttachedParameters, Is.Empty);

            Assert.That(await cursor.MoveNextAsync(), Is.False);
            Assert.That(cursor.Completed);
        }

        [Test]
        public async Task MoveNextAsync_ShouldReportRemainingPathForRootPrefix()
        {
            HandlerRegistration handler = new(s_handler, "/*");

            RouteNode root = new();
            root.HandlerRegistrations[HttpVerb.Get] = [handler];

            RouteMatchCursor cursor = CreateCursor(root, "/api/health");
            Assert.That(cursor.Completed, Is.False);

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.Current.HandlerRegistration, Is.EqualTo(handler));
            Assert.That(cursor.Current.RemainingPath.ToString(), Is.EqualTo("/api/health"));
            Assert.That(cursor.Current.AttachedParameters, Is.Empty);

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
                parsed = new()
                {
                    ParameterParser = new ParameterParser
                    (
                        Parse("{id:str}"),
                        static context => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())),
                        null
                    )
                };

            literal.HandlerRegistrations[HttpVerb.Get] = [literalHandler];
            parsed.HandlerRegistrations[HttpVerb.Get] = [parsedHandler];
            items.LiteralChildren.Add("value".AsMemory(), literal);
            items.ParsedChildren.Add(parsed);
            root.LiteralChildren.Add("items".AsMemory(), items);

            RouteMatchCursor cursor = CreateCursor(root, "/items/value", matchingPrecedence);

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.Current.HandlerRegistration.Pattern, Is.EqualTo(expectedPattern));

            if (matchingPrecedence is MatchingPrecedence.ParameterizedFirst)
                Assert.That(cursor.Current.AttachedParameters, Does.ContainKey("id").WithValue("value"));

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

            HandlerRegistration handler = new(s_handler, "/api/{slug:str}/details/");

            RouteNode
                root = new(),
                api = new(),
                intNode = new()
                {
                    ParameterParser = new ParameterParser
                    (
                        Parse("{id:int}"),
                        mockIntParser.Object,
                        null
                    )
                },
                stringNode = new()
                {
                    ParameterParser = new ParameterParser
                    (
                        Parse("{slug:str}"),
                        mockStringParser.Object,
                        null
                    )
                },
                details = new();

            details.HandlerRegistrations[HttpVerb.Get] = [handler];
            stringNode.LiteralChildren.Add("details".AsMemory(), details);
            api.ParsedChildren.Add(intNode);
            api.ParsedChildren.Add(stringNode);
            root.LiteralChildren.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/abc/details");

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.Current.HandlerRegistration, Is.EqualTo(handler));
            Assert.That(cursor.Current.AttachedParameters, Does.ContainKey("slug").WithValue("abc"));
            Assert.That(cursor.Current.AttachedParameters, Does.Not.ContainKey("id"));

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
                parsed = new()
                {
                    ParameterParser = new ParameterParser
                    (
                        Parse("{str}"),
                        static context => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())),
                        null
                    )
                },
                details = new();

            details.HandlerRegistrations[HttpVerb.Get] = [handler];
            parsed.LiteralChildren.Add("details".AsMemory(), details);
            api.ParsedChildren.Add(parsed);
            root.LiteralChildren.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/value/details");

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.Current.HandlerRegistration.Pattern, Is.EqualTo(handler.Pattern));
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

            HandlerRegistration handler = new(s_handler, "/api/{id:int}/details/");

            RouteNode
                root = new(),
                api = new(),
                intNode = new()
                {
                    ParameterParser = new ParameterParser
                    (
                        Parse("{id:int}"),
                        mockIntParser.Object,
                        null
                    )
                },
                stringNode = new()
                {
                    ParameterParser = new ParameterParser
                    (
                        Parse("{slug:str}"),
                        mockStringParser.Object,
                        null
                    )
                },
                details = new();

            details.HandlerRegistrations[HttpVerb.Get] = [handler];
            intNode.LiteralChildren.Add("details".AsMemory(), details);
            stringNode.LiteralChildren.Add("details".AsMemory(), new RouteNode());
            api.ParsedChildren.Add(intNode);
            api.ParsedChildren.Add(stringNode);
            root.LiteralChildren.Add("api".AsMemory(), api);

            RouteMatchCursor cursor = CreateCursor(root, "/api/1986/details");

            Assert.That(await cursor.MoveNextAsync(), Is.True);
            Assert.That(cursor.Current.HandlerRegistration.Pattern, Is.EqualTo(handler.Pattern));
            Assert.That(cursor.Current.AttachedParameters, Does.ContainKey("id").WithValue(1986));

            Assert.That(await cursor.MoveNextAsync(), Is.False);

            mockIntParser.Verify(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "1986")), Times.Once);
            mockStringParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Never);
        }
    }
}

