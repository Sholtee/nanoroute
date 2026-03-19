/********************************************************************************
* StringSegmentTests.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class StringSegmentTests
    {
        [TestCase("")]
        [TestCase("/")]
        [TestCase("/cica")]
        [TestCase("/cica/")]
        [TestCase("/cica/mica")]
        [TestCase("/cica/mica/")]
        [TestCase("a/b/b/d")]
        [TestCase("//a//b//")]
        public void Enumerate_ShouldReturnTheSplitSegments(string s)
        {
            Assert.That(new StringSegment(s, '/').Enumerate(), Is.EquivalentTo(s.Split(['/'], StringSplitOptions.RemoveEmptyEntries)));
        }
    }
}
