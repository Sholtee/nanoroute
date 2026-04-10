/********************************************************************************
* UriSegmentTests.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;

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
            UriSegment segment = new(s);

            Assert.That(ReadSegments(segment), Is.EquivalentTo(s.Split([UriSegment.SEPARATOR], StringSplitOptions.RemoveEmptyEntries)));

            static string[] ReadSegments(UriSegment segment)
            {
                System.Collections.Generic.List<string> result = [];

                while (segment.MoveNext())
                    result.Add(segment.Current.ToString());

                return [..result];
            }
        }
    }
}
