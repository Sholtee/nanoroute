/********************************************************************************
* RouteNodeTests.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class RouteNodeTests
    {
        private static ParameterDefinition Parse(string segment)
        {
            int offset = 0;
            ParameterDefinition definition = ParameterDefinition.Parse(segment, ref offset);

            Assert.That(offset, Is.EqualTo(segment.Length - 1));

            return definition;
        }

        private static KeyValuePair<ParameterParser, RouteNode> ParsedBranch(string segment, RouteNode node) => new
        (
            new ParameterParser
            (
                Parse(segment),
                new Mock<ValueParserDelegate>(MockBehavior.Strict).Object,
                null
            ),
            node
        );

        private static void AddHandler(RouteNode node, HttpVerb verb, HandlerRegistration handler) =>
            node.Handlers.Add(new KeyValuePair<HttpVerb, HandlerRegistration>(verb, handler));

        [Test]
        public void Freeze_ShouldCloneTheWholeTree()
        {
            RequestHandlerDelegate
                rootHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object,
                literalHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object,
                parsedHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object;

            RouteNode root = new();
            AddHandler(root, HttpVerb.Get, new HandlerRegistration(rootHandler, "/"));

            RouteNode literalBranch = new();
            AddHandler(literalBranch, HttpVerb.Post, new HandlerRegistration(literalHandler, "/users/"));
            root.LiteralBranches.Add("users".AsMemory(), literalBranch);

            RouteNode parsedBranch = new();
            AddHandler(parsedBranch, HttpVerb.Get, new HandlerRegistration(parsedHandler, "/{id:str}/"));
            root.ParsedBranches.Add(ParsedBranch("{id:str}", parsedBranch));

            RouteNode snapshot = root.Freeze();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot, Is.Not.SameAs(root));
                Assert.That(snapshot.Frozen, Is.True);

                Assert.That(snapshot.Handlers, Is.Not.SameAs(root.Handlers));
                Assert.That(snapshot.Handlers, Is.InstanceOf<ImmutableArray<KeyValuePair<HttpVerb, HandlerRegistration>>>());
                Assert.That(snapshot.Handlers, Is.EqualTo(root.Handlers));

                Assert.That(snapshot.LiteralBranches, Is.Not.SameAs(root.LiteralBranches));
                Assert.That(snapshot.LiteralBranches, Is.InstanceOf<FrozenDictionary<ReadOnlyMemory<char>, RouteNode>>());
                Assert.That(snapshot.LiteralBranches["users".AsMemory()], Is.Not.SameAs(literalBranch));
                Assert.That(snapshot.LiteralBranches["users".AsMemory()].Frozen, Is.True);
                Assert.That(snapshot.LiteralBranches["users".AsMemory()].Handlers, Is.EqualTo(literalBranch.Handlers));

                Assert.That(snapshot.ParsedBranches, Is.Not.SameAs(root.ParsedBranches));
                Assert.That(snapshot.ParsedBranches, Is.InstanceOf<ImmutableArray<KeyValuePair<ParameterParser, RouteNode>>>());
                Assert.That(snapshot.ParsedBranches, Has.Count.EqualTo(1));
                Assert.That(snapshot.ParsedBranches[0].Key, Is.EqualTo(root.ParsedBranches[0].Key));
                Assert.That(snapshot.ParsedBranches[0].Value, Is.Not.SameAs(parsedBranch));
                Assert.That(snapshot.ParsedBranches[0].Value.Frozen, Is.True);
                Assert.That(snapshot.ParsedBranches[0].Value.Handlers, Is.EqualTo(parsedBranch.Handlers));
            });
        }

        [Test]
        public void Freeze_ShouldFreezeTheWholeTree()
        {
            RouteNode root = new();

            RequestHandlerDelegate handler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object;
            AddHandler(root, HttpVerb.Get, new HandlerRegistration(handler, "/"));

            RouteNode literalBranch = new();
            AddHandler(literalBranch, HttpVerb.Post, new HandlerRegistration(handler, "/users/"));
            root.LiteralBranches.Add("users".AsMemory(), literalBranch);

            RouteNode parsedBranch = new();
            AddHandler(parsedBranch, HttpVerb.Get, new HandlerRegistration(handler, "/{id:str}/"));
            root.ParsedBranches.Add(ParsedBranch("{id:str}", parsedBranch));

            RouteNode snapshot = root.Freeze();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Handlers, Is.InstanceOf<ImmutableArray<KeyValuePair<HttpVerb, HandlerRegistration>>>());
                Assert.That(snapshot.LiteralBranches, Is.InstanceOf<FrozenDictionary<ReadOnlyMemory<char>, RouteNode>>());
                Assert.That(snapshot.ParsedBranches, Is.InstanceOf<ImmutableArray<KeyValuePair<ParameterParser, RouteNode>>>());

                Assert.That(snapshot.LiteralBranches["users".AsMemory()].Handlers, Is.InstanceOf<ImmutableArray<KeyValuePair<HttpVerb, HandlerRegistration>>>());
                Assert.That(snapshot.ParsedBranches[0].Value.Handlers, Is.InstanceOf<ImmutableArray<KeyValuePair<HttpVerb, HandlerRegistration>>>());
            });
        }

        [Test]
        public void Freeze_SnapshotShouldStayIndependentFromLaterMutations()
        {
            RequestHandlerDelegate
                initialHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object,
                addedLaterHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object;

            RouteNode root = new();
            AddHandler(root, HttpVerb.Get, new HandlerRegistration(initialHandler, "/"));

            RouteNode literalBranch = new();
            root.LiteralBranches.Add("users".AsMemory(), literalBranch);

            RouteNode snapshot = root.Freeze();

            AddHandler(root, HttpVerb.Get, new HandlerRegistration(addedLaterHandler, "/after/"));
            root.LiteralBranches.Add("admins".AsMemory(), new RouteNode());
            root.ParsedBranches.Add(ParsedBranch("{id:str}", new RouteNode()));

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Handlers, Has.Count.EqualTo(1));
                Assert.That(snapshot.Handlers[0].Key, Is.EqualTo(HttpVerb.Get));
                Assert.That(snapshot.Handlers[0].Value.Pattern, Is.EqualTo("/"));
                Assert.That(snapshot.LiteralBranches.ContainsKey("admins".AsMemory()), Is.False);
                Assert.That(snapshot.ParsedBranches, Is.Empty);
            });
        }

        [Test]
        public void Freeze_ShouldSetSingleBranch_WhenSnapshotHasOnlyOneLiteralBranch()
        {
            RouteNode
                literalRoot = new(),
                literalBranch = new();

            literalRoot.LiteralBranches.Add("users".AsMemory(), literalBranch);

            RouteNode snapshot = literalRoot.Freeze();

            Assert.That(snapshot.SingleBranch, Is.EqualTo(snapshot.LiteralBranches.Single()));
        }

        [Test]
        public void Freeze_ShouldSetSingleBranch_WhenSnapshotHasOnlyOneParsedBranch()
        {
            RouteNode
                parsedRoot = new(),
                parsedBranch = new();

            parsedRoot.ParsedBranches.Add(ParsedBranch("{id:str}", parsedBranch));

            RouteNode snapshot = parsedRoot.Freeze();

            Assert.That(snapshot.SingleBranch, Is.EqualTo(snapshot.ParsedBranches[0]));
        }

        [Test]
        public void Freeze_ShouldNotSetSingleBranch_WhenSnapshotHasHandlers()
        {
            RouteNode withHandler = new();
            AddHandler(withHandler, HttpVerb.Get, new HandlerRegistration(new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object, "/"));
            withHandler.LiteralBranches.Add("users".AsMemory(), new RouteNode());

            Assert.That(withHandler.Freeze().SingleBranch is null, Is.True);
        }

        [Test]
        public void Freeze_ShouldNotSetSingleBranch_WhenSnapshotHasMultipleBranchKinds()
        {
            RouteNode withMixedBranches = new();
            withMixedBranches.LiteralBranches.Add("users".AsMemory(), new RouteNode());
            withMixedBranches.ParsedBranches.Add(ParsedBranch("{id:str}", new RouteNode()));

            Assert.That(withMixedBranches.Freeze().SingleBranch is null, Is.True);
        }
    }
}
