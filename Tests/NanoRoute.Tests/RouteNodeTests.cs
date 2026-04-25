/********************************************************************************
* RouteNodeTests.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Collections.Immutable;

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

        [Test]
        public void Copy_ShouldCloneTheWholeTree_WhenMutableCopyIsRequested()
        {
            ParameterParser parser = new
            (
                Parse("{id:str}"),
                new Mock<ValueParserDelegate>(MockBehavior.Strict).Object,
                Arguments: null
            );

            RequestHandlerDelegate
                rootHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object,
                literalHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object,
                parsedHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object;

            RouteNode root = new();
            root.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(rootHandler, "/")];

            RouteNode literalChild = new();
            literalChild.HandlerRegistrations[HttpVerb.Post] = [new HandlerRegistration(literalHandler, "/users")];
            root.LiteralChildren.Add("users".AsMemory(), literalChild);

            RouteNode parsedChild = new() { ParameterParser = parser };
            parsedChild.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(parsedHandler, "/{id:str}")];
            root.ParsedChildren.Add(parsedChild);

            RouteNode copy = root.Copy(frozen: false);

            Assert.Multiple(() =>
            {
                Assert.That(copy, Is.Not.SameAs(root));

                Assert.That(copy.HandlerRegistrations, Is.Not.SameAs(root.HandlerRegistrations));
                Assert.That(copy.HandlerRegistrations, Is.TypeOf<Dictionary<HttpVerb, IList<HandlerRegistration>>>());
                Assert.That(copy.HandlerRegistrations[HttpVerb.Get], Is.Not.SameAs(root.HandlerRegistrations[HttpVerb.Get]));
                Assert.That(copy.HandlerRegistrations[HttpVerb.Get], Is.EquivalentTo(root.HandlerRegistrations[HttpVerb.Get]));
                Assert.That(copy.HandlerRegistrations[HttpVerb.Get], Is.TypeOf<List<HandlerRegistration>>());

                Assert.That(copy.LiteralChildren, Is.Not.SameAs(root.LiteralChildren));
                Assert.That(copy.LiteralChildren, Is.TypeOf<Dictionary<ReadOnlyMemory<char>, RouteNode>>());
                Assert.That(copy.LiteralChildren["users".AsMemory()], Is.Not.SameAs(literalChild));
                Assert.That(copy.LiteralChildren["users".AsMemory()].HandlerRegistrations[HttpVerb.Post], Is.EquivalentTo(literalChild.HandlerRegistrations[HttpVerb.Post]));

                Assert.That(copy.ParsedChildren, Is.Not.SameAs(root.ParsedChildren));
                Assert.That(copy.ParsedChildren, Is.TypeOf<List<RouteNode>>());
                Assert.That(copy.ParsedChildren, Has.Count.EqualTo(1));
                Assert.That(copy.ParsedChildren[0], Is.Not.SameAs(parsedChild));
                Assert.That(copy.ParsedChildren[0].ParameterParser, Is.EqualTo(parser));
                Assert.That(copy.ParsedChildren[0].HandlerRegistrations[HttpVerb.Get], Is.EquivalentTo(parsedChild.HandlerRegistrations[HttpVerb.Get]));
            });
        }

        [Test]
        public void Copy_ShouldFreezeTheWholeTree_WhenFrozenCopyIsRequested()
        {
            RouteNode root = new();

            RequestHandlerDelegate handler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object;
            root.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(handler, "/")];

            RouteNode literalChild = new();
            literalChild.HandlerRegistrations[HttpVerb.Post] = [new HandlerRegistration(handler, "/users")];
            root.LiteralChildren.Add("users".AsMemory(), literalChild);

            RouteNode parsedChild = new()
            {
                ParameterParser = new ParameterParser
                (
                    Parse("{id:str}"),
                    new Mock<ValueParserDelegate>(MockBehavior.Strict).Object,
                    Arguments: null
                )
            };
            parsedChild.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(handler, "/{id:str}")];
            root.ParsedChildren.Add(parsedChild);

            RouteNode copy = root.Copy(frozen: true);

            Assert.Multiple(() =>
            {
                Assert.That(copy.HandlerRegistrations, Is.InstanceOf<FrozenDictionary<HttpVerb, IList<HandlerRegistration>>>());
                Assert.That(copy.LiteralChildren, Is.InstanceOf<FrozenDictionary<ReadOnlyMemory<char>, RouteNode>>());
                Assert.That(copy.ParsedChildren, Is.InstanceOf<ImmutableArray<RouteNode>>());

                Assert.That(copy.LiteralChildren["users".AsMemory()].HandlerRegistrations, Is.InstanceOf<FrozenDictionary<HttpVerb, IList<HandlerRegistration>>>());
                Assert.That(copy.ParsedChildren[0].HandlerRegistrations, Is.InstanceOf<FrozenDictionary<HttpVerb, IList<HandlerRegistration>>>());
            });
        }

        [Test]
        public void Copy_SnapshotShouldStayIndependentFromLaterMutations([Values] bool frozen)
        {
            RequestHandlerDelegate
                initialHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object,
                addedLaterHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object;

            RouteNode root = new();
            root.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(initialHandler, "/")];

            RouteNode literalChild = new();
            root.LiteralChildren.Add("users".AsMemory(), literalChild);

            RouteNode snapshot = root.Copy(frozen);

            root.HandlerRegistrations[HttpVerb.Get].Add(new HandlerRegistration(addedLaterHandler, "/after"));
            root.LiteralChildren.Add("admins".AsMemory(), new RouteNode());
            root.ParsedChildren.Add(new RouteNode());

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.HandlerRegistrations[HttpVerb.Get], Has.Count.EqualTo(1));
                Assert.That(snapshot.HandlerRegistrations[HttpVerb.Get][0].Pattern, Is.EqualTo("/"));
                Assert.That(snapshot.LiteralChildren.ContainsKey("admins".AsMemory()), Is.False);
                Assert.That(snapshot.ParsedChildren, Is.Empty);
            });
        }
    }
}
