/********************************************************************************
* RouterConfigTests.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    [TestFixture]
    internal sealed class RouterConfigTests
    {
        [Test]
        public void Ctor_ShouldRejectUnknownMatchingPrecedence()
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>
            (
                () => new RouterConfig { MatchingPrecedence = (MatchingPrecedence) 100 }
            )!;

            Assert.That(ex.ParamName, Is.EqualTo("value"));
        }
    }
}
