/********************************************************************************
* UrlUtilsTests.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class UrlUtilsTests
    {
        private static string Decode(string source)
        {
            char[] buffer = new char[source.Length];

            Assert.That(UrlUtils.TryDecodeUrl(source, buffer, out int charsWritten), Is.True);
            return new string(buffer, 0, charsWritten);
        }

        [TestCase("", "")]
        [TestCase("plain", "plain")]
        [TestCase("hello+world", "hello world")]
        [TestCase("a%20b%2Fc", "a b/c")]
        [TestCase("%C3%A1", "á")]
        [TestCase("%F0%9F%98%80", "😀")]
        [TestCase("caf%C3%A9+%F0%9F%98%80", "café 😀")]
        public void TryDecodeUrl_ShouldDecodeValidInputs(string source, string expected)
        {
            Assert.That(Decode(source), Is.EqualTo(expected));
        }

        [TestCase("%")]
        [TestCase("%2")]
        [TestCase("%GG")]
        [TestCase("%u00E1")]
        [TestCase("%u123")]
        [TestCase("%u12GG")]
        [TestCase("%80")]
        [TestCase("%C3")]
        [TestCase("%E0%80")]
        [TestCase("%C0%AF")]
        [TestCase("%E0%80%80")]
        [TestCase("%F4%90%80%80")]
        public void TryDecodeUrl_ShouldRejectInvalidEscapes(string source)
        {
            char[] buffer = new char[source.Length];

            Assert.That(UrlUtils.TryDecodeUrl(source, buffer, out _), Is.False);
        }

        [Test]
        public void TryDecodeUrl_ShouldRejectWhenDestinationIsTooSmall()
        {
            Span<char> buffer = stackalloc char[2];

            Assert.That(UrlUtils.TryDecodeUrl("abcd", buffer, out _), Is.False);
        }
    }
}
