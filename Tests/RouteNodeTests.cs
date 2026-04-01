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
            ParameterParserDelegate parserDelegate = (string segment, out object? parsed) =>
            {
                parsed = segment;
                return true;
            };

            ParameterParser parser = new("str", parserDelegate) { ParameterName = "id" };

            RequestHandlerDelegate
                rootHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object,
                literalHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object,
                parameterizedHandler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object;

            RouteNode root = new() { Segment = string.Empty };
            root.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(rootHandler, "/")];

            RouteNode literalChild = new() { Segment = "users" };
            literalChild.HandlerRegistrations[HttpVerb.Post] = [new HandlerRegistration(literalHandler, "/users")];
            root.LiteralChildren.Add("users", literalChild);

            RouteNode parameterizedChild = new() { Segment = "{id:str}", ParameterParser = parser };
            parameterizedChild.HandlerRegistrations[HttpVerb.Get] = [new HandlerRegistration(parameterizedHandler, "/{id:str}")];
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
