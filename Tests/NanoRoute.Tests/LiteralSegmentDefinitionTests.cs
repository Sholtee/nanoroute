/********************************************************************************
* LiteralSegmentDefinitionTests.cs                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;
    using Properties;

    [TestFixture]
    internal sealed class LiteralSegmentDefinitionTests
    {
        [TestCase("/items/details", 1, "items", 5)]
        [TestCase("alpha%20beta/cica", 0, "alpha beta", 11)]
        [TestCase("alpha+beta/cica", 0, "alpha+beta", 9)]
        public void Parse_ShouldConsumeLiteralSegments(string pattern, int offset, string expectedSegment, int expectedNewOffset)
        {
            ReadOnlyMemory<char> segment = LiteralSegmentDefinition.Parse(pattern, ref offset);

            Assert.That(segment.ToString(), Is.EqualTo(expectedSegment));
            Assert.That(offset, Is.EqualTo(expectedNewOffset));
        }

        [TestCase("{value:int}", 0)]
        [TestCase("%2X", 0)]
        public void Parse_ShouldRejectWhenNoLiteralStartsAtOffset(string pattern, int offset)
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => LiteralSegmentDefinition.Parse(pattern, ref offset))!;

            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, offset)));
        }

        [Test]
        public void Parse_ShouldStopBeforeInvalidTail()
        {
            int offset = 0;
            ReadOnlyMemory<char> segment = LiteralSegmentDefinition.Parse("bad[segment]", ref offset);

            Assert.That(segment.ToString(), Is.EqualTo("bad"));
            Assert.That(offset, Is.EqualTo(2));
        }
    }
}
