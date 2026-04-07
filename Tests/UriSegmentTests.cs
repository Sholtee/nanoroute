/********************************************************************************
* UriSegmentTests.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class UriSegmentTests
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
            Assert.That(new UriSegment(s).Enumerate(), Is.EquivalentTo(s.Split([UriSegment.SEPARATOR], StringSplitOptions.RemoveEmptyEntries)));
        }
    }
}
