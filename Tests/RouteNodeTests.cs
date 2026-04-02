/********************************************************************************
* RouteNodeTests.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
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
            ParameterParser parser = new("str", new Mock<ParameterParserDelegate>(MockBehavior.Strict).Object)
            {
                ParameterName = "id"
            };

            RouteNode root = new() { Segment = string.Empty };
            root.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object, "/")];

            RouteNode literalChild = new() { Segment = "users" };
            literalChild.HandlerRegistrations[HttpVerb.Post] = [new HandlerRegistration(new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object, "/users")];
            root.LiteralChildren.Add("users", literalChild);

            RouteNode parameterizedChild = new() { Segment = "{id:str}", ParameterParser = parser };
            parameterizedChild.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object, "/{id:str}")];
            root.ParameterizedChildren.Add(parameterizedChild);

            RouteNode copy = root.Copy();

            Assert.Multiple(() =>
            {
                Assert.That(copy, Is.Not.SameAs(root));
                Assert.That(copy.Segment, Is.EqualTo(root.Segment));

                Assert.That(copy.HandlerRegistrations, Is.Not.SameAs(root.HandlerRegistrations));
                Assert.That(copy.HandlerRegistrations[HttpVerb.Get], Is.Not.SameAs(root.HandlerRegistrations[HttpVerb.Get]));
                Assert.That(copy.HandlerRegistrations[HttpVerb.Get], Is.EquivalentTo(root.HandlerRegistrations[HttpVerb.Get]));

                Assert.That(copy.LiteralChildren, Is.Not.SameAs(root.LiteralChildren));
                Assert.That(copy.LiteralChildren["users"], Is.Not.SameAs(literalChild));
                Assert.That(copy.LiteralChildren["users"].Segment, Is.EqualTo("users"));
                Assert.That(copy.LiteralChildren["users"].HandlerRegistrations[HttpVerb.Post], Is.EquivalentTo(literalChild.HandlerRegistrations[HttpVerb.Post]));

                Assert.That(copy.ParameterizedChildren, Is.Not.SameAs(root.ParameterizedChildren));
                Assert.That(copy.ParameterizedChildren, Has.Count.EqualTo(1));
                Assert.That(copy.ParameterizedChildren[0], Is.Not.SameAs(parameterizedChild));
                Assert.That(copy.ParameterizedChildren[0].Segment, Is.EqualTo("{id:str}"));
                Assert.That(copy.ParameterizedChildren[0].ParameterParser, Is.EqualTo(parser));
                Assert.That(copy.ParameterizedChildren[0].HandlerRegistrations[HttpVerb.Get], Is.EquivalentTo(parameterizedChild.HandlerRegistrations[HttpVerb.Get]));
            });
        }
    }
}
