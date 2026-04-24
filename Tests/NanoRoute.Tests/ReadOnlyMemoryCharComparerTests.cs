/********************************************************************************
* ReadOnlyMemoryCharComparerTests.cs                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class ReadOnlyMemoryCharComparerTests
    {
        private static readonly ReadOnlyMemoryCharComparer s_comparer = ReadOnlyMemoryCharComparer.Instance;

        [TestCase("", "", true)]
        [TestCase("route", "ROUTE", true)]
        [TestCase("café", "CAFÉ", true)]
        [TestCase("ı", "I", false)]
        [TestCase("ß", "ẞ", false)]
        [TestCase("café", "cafe\u0301", false)]
        public void Equals_ShouldUseOrdinalIgnoreCaseSemantics(string left, string right, bool expected)
        {
            Assert.That(s_comparer.Equals(left.AsMemory(), right.AsMemory()), Is.EqualTo(expected));
            Assert.That(s_comparer.Equals(right.AsMemory(), left.AsMemory()), Is.EqualTo(expected));
        }

        [TestCase("route", "ROUTE")]
        [TestCase("café", "CAFÉ")]
        public void GetHashCode_ShouldReturnSameHashWhenValuesAreEqual(string left, string right)
        {
            Assert.That(s_comparer.Equals(left.AsMemory(), right.AsMemory()), Is.True);
            Assert.That(s_comparer.GetHashCode(left.AsMemory()), Is.EqualTo(s_comparer.GetHashCode(right.AsMemory())));
        }

#if NET10_0_OR_GREATER
        [TestCase("κόσμος", "ΚΌΣΜΟΣ")]
        [TestCase("ς", "σ")]
        public void Equals_ShouldHandleExtendedUnicodeCasePairs_WhenTheRuntimeSupportsThem(string left, string right)
        {
            Assert.That(s_comparer.Equals(left.AsMemory(), right.AsMemory()), Is.True);
            Assert.That(s_comparer.Equals(right.AsMemory(), left.AsMemory()), Is.True);
        }

        [TestCase("κόσμος", "ΚΌΣΜΟΣ")]
        [TestCase("ς", "σ")]
        public void GetHashCode_ShouldMatchForExtendedUnicodeCasePairs_WhenTheRuntimeSupportsThem(string left, string right)
        {
            Assert.That(s_comparer.Equals(left.AsMemory(), right.AsMemory()), Is.True);
            Assert.That(s_comparer.GetHashCode(left.AsMemory()), Is.EqualTo(s_comparer.GetHashCode(right.AsMemory())));
        }
#endif

        [Test]
        public void Equals_ShouldHandleSlicedMemory()
        {
            ReadOnlyMemory<char>
                left = "xxcaféyy".AsMemory(2, 4),
                right = "--CAFÉ++".AsMemory(2, 4);

            Assert.That(s_comparer.Equals(left, right), Is.True);
            Assert.That(s_comparer.GetHashCode(left), Is.EqualTo(s_comparer.GetHashCode(right)));
        }

        [Test]
        public void Dictionary_ShouldFindNonAsciiKeyIgnoringCase()
        {
            Dictionary<ReadOnlyMemory<char>, int> dictionary = new(s_comparer)
            {
                ["café".AsMemory()] = 42
            };

            Assert.That(dictionary.ContainsKey("CAFÉ".AsMemory()), Is.True);
        }
    }
}
