/********************************************************************************
* DelimitedSegmentTests.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class DelimitedSegmentTests
    {
        private static IEnumerable<string> ReadSegments(DelimitedSegment segment)
        {
            while (segment.MoveNext())
                yield return segment.Current.ToString();
        }

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
            using DelimitedSegment segment = new(s.AsMemory(), '/');

            Assert.That(ReadSegments(segment), Is.EquivalentTo(s.Split(['/'], StringSplitOptions.RemoveEmptyEntries)));
        }

        [TestCase("a&b&c", '&', new[] { "a", "b", "c" })]
        [TestCase("&&a&&b&&", '&', new[] { "a", "b" })]
        [TestCase("a=b=c", '=', new[] { "a", "b", "c" })]
        public void Enumerate_ShouldRespectCustomSeparator(string input, char separator, string[] expected)
        {
            using DelimitedSegment segment = new(input.AsMemory(), separator);

            Assert.That(ReadSegments(segment), Is.EquivalentTo(expected));
        }

        [Test]
        public void Remaining_ShouldReturnUnenumeratedTailWithSeparator()
        {
            using DelimitedSegment segment = new("/api/health/status".AsMemory(), '/');

            Assert.That(segment.Remaining.ToString(), Is.EqualTo("/api/health/status"));

            Assert.That(segment.MoveNext(), Is.True);
            Assert.That(segment.Current.ToString(), Is.EqualTo("api"));
            Assert.That(segment.Remaining.ToString(), Is.EqualTo("/health/status"));

            Assert.That(segment.MoveNext(), Is.True);
            Assert.That(segment.Current.ToString(), Is.EqualTo("health"));
            Assert.That(segment.Remaining.ToString(), Is.EqualTo("/status"));

            Assert.That(segment.MoveNext(), Is.True);
            Assert.That(segment.Current.ToString(), Is.EqualTo("status"));
            Assert.That(segment.Remaining.ToString(), Is.Empty);

            Assert.That(segment.MoveNext(), Is.False);
            Assert.That(segment.Remaining.ToString(), Is.Empty);
        }

        [Test]
        public void Remaining_ShouldRespectCustomSeparator()
        {
            using DelimitedSegment segment = new("a&b&c".AsMemory(), '&');

            Assert.That(segment.Remaining.ToString(), Is.EqualTo("a&b&c"));

            Assert.That(segment.MoveNext(), Is.True);
            Assert.That(segment.Current.ToString(), Is.EqualTo("a"));
            Assert.That(segment.Remaining.ToString(), Is.EqualTo("&b&c"));

            Assert.That(segment.MoveNext(), Is.True);
            Assert.That(segment.Current.ToString(), Is.EqualTo("b"));
            Assert.That(segment.Remaining.ToString(), Is.EqualTo("&c"));

            Assert.That(segment.MoveNext(), Is.True);
            Assert.That(segment.Current.ToString(), Is.EqualTo("c"));
            Assert.That(segment.Remaining.ToString(), Is.Empty);
        }

        [Test]
        public void Reset_ShouldAllowRestartingEnumeration()
        {
            using DelimitedSegment segment = new("a&b&c".AsMemory(), '&');

            IEnumerator enumerator = segment;

            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current?.ToString(), Is.EqualTo("a"));

            enumerator.Reset();

            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current?.ToString(), Is.EqualTo("a"));
        }
    }
}
