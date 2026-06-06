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

        [Test]
        public void Freeze_ShouldCloneTheWholeTree()
        {
            RequestHandlerDelegate
                rootHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object,
                literalHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object,
                parsedHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object;

            RouteNode root = new();
            root.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(rootHandler, "/")];

            RouteNode literalBranch = new();
            literalBranch.HandlerRegistrations[HttpVerb.Post] = [new HandlerRegistration(literalHandler, "/users/")];
            root.LiteralBranches.Add("users".AsMemory(), literalBranch);

            RouteNode parsedBranch = new();
            parsedBranch.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(parsedHandler, "/{id:str}/")];
            root.ParsedBranches.Add(ParsedBranch("{id:str}", parsedBranch));

            RouteNode snapshot = root.Freeze();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot, Is.Not.SameAs(root));
                Assert.That(snapshot.Frozen, Is.True);

                Assert.That(snapshot.HandlerRegistrations, Is.Not.SameAs(root.HandlerRegistrations));
                Assert.That(snapshot.HandlerRegistrations, Is.InstanceOf<FrozenDictionary<HttpVerb, IList<HandlerRegistration>>>());
                Assert.That(snapshot.HandlerRegistrations[HttpVerb.Get], Is.Not.SameAs(root.HandlerRegistrations[HttpVerb.Get]));
                Assert.That(snapshot.HandlerRegistrations[HttpVerb.Get], Is.EquivalentTo(root.HandlerRegistrations[HttpVerb.Get]));
                Assert.That(snapshot.HandlerRegistrations[HttpVerb.Get], Is.InstanceOf<ImmutableArray<HandlerRegistration>>());

                Assert.That(snapshot.LiteralBranches, Is.Not.SameAs(root.LiteralBranches));
                Assert.That(snapshot.LiteralBranches, Is.InstanceOf<FrozenDictionary<ReadOnlyMemory<char>, RouteNode>>());
                Assert.That(snapshot.LiteralBranches["users".AsMemory()], Is.Not.SameAs(literalBranch));
                Assert.That(snapshot.LiteralBranches["users".AsMemory()].Frozen, Is.True);
                Assert.That(snapshot.LiteralBranches["users".AsMemory()].HandlerRegistrations[HttpVerb.Post], Is.EquivalentTo(literalBranch.HandlerRegistrations[HttpVerb.Post]));

                Assert.That(snapshot.ParsedBranches, Is.Not.SameAs(root.ParsedBranches));
                Assert.That(snapshot.ParsedBranches, Is.InstanceOf<ImmutableArray<KeyValuePair<ParameterParser, RouteNode>>>());
                Assert.That(snapshot.ParsedBranches, Has.Count.EqualTo(1));
                Assert.That(snapshot.ParsedBranches[0].Key, Is.EqualTo(root.ParsedBranches[0].Key));
                Assert.That(snapshot.ParsedBranches[0].Value, Is.Not.SameAs(parsedBranch));
                Assert.That(snapshot.ParsedBranches[0].Value.Frozen, Is.True);
                Assert.That(snapshot.ParsedBranches[0].Value.HandlerRegistrations[HttpVerb.Get], Is.EquivalentTo(parsedBranch.HandlerRegistrations[HttpVerb.Get]));
            });
        }

        [Test]
        public void Freeze_ShouldFreezeTheWholeTree()
        {
            RouteNode root = new();

            RequestHandlerDelegate handler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object;
            root.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(handler, "/")];

            RouteNode literalBranch = new();
            literalBranch.HandlerRegistrations[HttpVerb.Post] = [new HandlerRegistration(handler, "/users/")];
            root.LiteralBranches.Add("users".AsMemory(), literalBranch);

            RouteNode parsedBranch = new();
            parsedBranch.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(handler, "/{id:str}/")];
            root.ParsedBranches.Add(ParsedBranch("{id:str}", parsedBranch));

            RouteNode snapshot = root.Freeze();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.HandlerRegistrations, Is.InstanceOf<FrozenDictionary<HttpVerb, IList<HandlerRegistration>>>());
                Assert.That(snapshot.LiteralBranches, Is.InstanceOf<FrozenDictionary<ReadOnlyMemory<char>, RouteNode>>());
                Assert.That(snapshot.ParsedBranches, Is.InstanceOf<ImmutableArray<KeyValuePair<ParameterParser, RouteNode>>>());

                Assert.That(snapshot.LiteralBranches["users".AsMemory()].HandlerRegistrations, Is.InstanceOf<FrozenDictionary<HttpVerb, IList<HandlerRegistration>>>());
                Assert.That(snapshot.ParsedBranches[0].Value.HandlerRegistrations, Is.InstanceOf<FrozenDictionary<HttpVerb, IList<HandlerRegistration>>>());
            });
        }

        [Test]
        public void Freeze_SnapshotShouldStayIndependentFromLaterMutations()
        {
            RequestHandlerDelegate
                initialHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object,
                addedLaterHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object;

            RouteNode root = new();
            root.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(initialHandler, "/")];

            RouteNode literalBranch = new();
            root.LiteralBranches.Add("users".AsMemory(), literalBranch);

            RouteNode snapshot = root.Freeze();

            root.HandlerRegistrations[HttpVerb.Get].Add(new HandlerRegistration(addedLaterHandler, "/after/"));
            root.LiteralBranches.Add("admins".AsMemory(), new RouteNode());
            root.ParsedBranches.Add(ParsedBranch("{id:str}", new RouteNode()));

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.HandlerRegistrations[HttpVerb.Get], Has.Count.EqualTo(1));
                Assert.That(snapshot.HandlerRegistrations[HttpVerb.Get][0].Pattern, Is.EqualTo("/"));
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
            withHandler.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object, "/")];
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
