/********************************************************************************
* RouteNodeTests.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class RouteNodeTests
    {
        [Test]
        public void Copy_ShouldCloneTheWholeTree()
        {
            SegmentParser parser = new(SegmentParserDefinition.Create("{id:str}"), new Mock<ValueParserDelegate>(MockBehavior.Strict).Object, Arguments: null);

            RouteNode root = new() { Segment = default };
            root.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object, "/")];

            RouteNode literalChild = new() { Segment = "users".AsMemory() };
            literalChild.HandlerRegistrations[HttpVerb.Post] = [new HandlerRegistration(new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object, "/users")];
            root.LiteralChildren.Add("users".AsMemory(), literalChild);

            RouteNode parsedChild = new() { Segment = "{id:str}".AsMemory(), SegmentParser = parser };
            parsedChild.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object, "/{id:str}")];
            root.ParsedChildren.Add(parsedChild);

            RouteNode copy = root.Copy();

            Assert.Multiple(() =>
            {
                Assert.That(copy, Is.Not.SameAs(root));
                Assert.That(copy.Segment, Is.EqualTo(root.Segment));

                Assert.That(copy.HandlerRegistrations, Is.Not.SameAs(root.HandlerRegistrations));
                Assert.That(copy.HandlerRegistrations[HttpVerb.Get], Is.Not.SameAs(root.HandlerRegistrations[HttpVerb.Get]));
                Assert.That(copy.HandlerRegistrations[HttpVerb.Get], Is.EquivalentTo(root.HandlerRegistrations[HttpVerb.Get]));

                Assert.That(copy.LiteralChildren, Is.Not.SameAs(root.LiteralChildren));
                Assert.That(copy.LiteralChildren["users".AsMemory()], Is.Not.SameAs(literalChild));
                Assert.That(copy.LiteralChildren["users".AsMemory()].Segment.ToString(), Is.EqualTo("users"));
                Assert.That(copy.LiteralChildren["users".AsMemory()].HandlerRegistrations[HttpVerb.Post], Is.EquivalentTo(literalChild.HandlerRegistrations[HttpVerb.Post]));

                Assert.That(copy.ParsedChildren, Is.Not.SameAs(root.ParsedChildren));
                Assert.That(copy.ParsedChildren, Has.Count.EqualTo(1));
                Assert.That(copy.ParsedChildren[0], Is.Not.SameAs(parsedChild));
                Assert.That(copy.ParsedChildren[0].Segment.ToString(), Is.EqualTo("{id:str}"));
                Assert.That(copy.ParsedChildren[0].SegmentParser, Is.EqualTo(parser));
                Assert.That(copy.ParsedChildren[0].HandlerRegistrations[HttpVerb.Get], Is.EquivalentTo(parsedChild.HandlerRegistrations[HttpVerb.Get]));
            });
        }
    }
}

