/********************************************************************************
* ValueParsersTests.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class ValueParsersTests
    {
        private static ReadOnlyMemory<char> AsMemory(string value) => value.AsMemory();

        [TestCase("0", 0)]
        [TestCase("42", 42)]
        [TestCase("-1", -1)]
        [TestCase("+7", 7)]
        [TestCase("2147483647", int.MaxValue)]
        [TestCase("-2147483648", int.MinValue)]
        public void TryParseInt32_ShouldAcceptValidValues(string value, int expected)
        {
            Assert.That(ValueParsers.TryParseInt32(AsMemory(value), out int result), Is.True);
            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase("+")]
        [TestCase("-")]
        [TestCase(" 1")]
        [TestCase("1 ")]
        [TestCase("1.0")]
        [TestCase("abc")]
        [TestCase("2147483648")]
        [TestCase("-2147483649")]
        public void TryParseInt32_ShouldRejectInvalidValues(string value)
        {
            Assert.That(ValueParsers.TryParseInt32(AsMemory(value), out _), Is.False);
        }

        [TestCase("true", true)]
        [TestCase("false", false)]
        [TestCase("TRUE", true)]
        [TestCase("False", false)]
        [TestCase("tRuE", true)]
        public void TryParseBoolean_ShouldAcceptValidValues(string value, bool expected)
        {
            Assert.That(ValueParsers.TryParseBoolean(AsMemory(value), out bool result), Is.True);
            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase("0")]
        [TestCase("1")]
        [TestCase("yes")]
        [TestCase(" true")]
        [TestCase("false ")]
        [TestCase("truth")]
        public void TryParseBoolean_ShouldRejectInvalidValues(string value)
        {
            Assert.That(ValueParsers.TryParseBoolean(AsMemory(value), out _), Is.False);
        }

        [TestCase("4a91f2c0-0e3c-4ec8-9f8c-8d2d2f2c7d1a")]
        [TestCase("4A91F2C0-0E3C-4EC8-9F8C-8D2D2F2C7D1A")]
        [TestCase("4a91f2c00e3c4ec89f8c8d2d2f2c7d1a")]
        [TestCase("4A91F2C00E3C4EC89F8C8D2D2F2C7D1A")]
        public void TryParseGuid_ShouldAcceptSupportedFormats(string value)
        {
            Guid expected = Guid.Parse(value);

            Assert.That(ValueParsers.TryParseGuid(AsMemory(value), out Guid result), Is.True);
            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase("4a91f2c0-0e3c-4ec8-9f8c-8d2d2f2c7d1")]
        [TestCase("4a91f2c0-0e3c-4ec8-9f8c--8d2d2f2c7d1a")]
        [TestCase("4a91f2c0-0e3c-4éc8-9f8c-8d2d2f2c7d1a")]
        [TestCase("4a91f2c0-0e3c-4ec8-9f8c-8d2d2f2c7d1aa")]
        [TestCase("4a91f2c00e3c4ec89f8c8d2d2f2c7d1")]
        [TestCase("4a91f2c00e3c4ec89f8c8d2d2f2c7d1aa")]
        [TestCase("4a91f2c0_0e3c-4ec8-9f8c-8d2d2f2c7d1a")]
        [TestCase("4a91f2c0-0e3c4ec8-9f8c-8d2d2f2c7d1a")]
        [TestCase("4a91f2c0-0e3c-4ec8-9f8c8d2d2f2c7d1a")]
        [TestCase("4a91f2c0-0e3c-4ec8-9f8c-8d2d2f2c7d1g")]
        [TestCase("4a91f2c00e3c4ec89f8c8d2d2f2c7d1g")]
        public void TryParseGuid_ShouldRejectInvalidValues(string value)
        {
            Assert.That(ValueParsers.TryParseGuid(AsMemory(value), out _), Is.False);
        }
    }
}
