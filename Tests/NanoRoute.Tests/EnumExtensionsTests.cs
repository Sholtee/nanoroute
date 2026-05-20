/********************************************************************************
* EnumExtensionsTests.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class EnumExtensionsTests
    {
        internal enum TestEnum
        {
            First,
            SecondValue,
            Third
        }

        [Test]
        public void Names_ShouldReturnEnumNames()
        {
            Assert.That(TestEnum.Names, Is.EquivalentTo(new[]
            {
                nameof(TestEnum.First),
                nameof(TestEnum.SecondValue),
                nameof(TestEnum.Third)
            }));
        }

        [TestCase("First", TestEnum.First)]
        [TestCase("first", TestEnum.First)]
        [TestCase("SECONDVALUE", TestEnum.SecondValue)]
        [TestCase("secondvalue", TestEnum.SecondValue)]
        public void TryParseFast_ShouldParseNamesIgnoringCase(string value, TestEnum expected)
        {
            Assert.That(TestEnum.TryParseFast(value, out TestEnum actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase("Invalid")]
        [TestCase("1")]
        public void TryParseFast_ShouldReturnFalse_WhenValueIsNotAnEnumName(string value)
        {
            Assert.That(TestEnum.TryParseFast(value, out TestEnum actual), Is.False);
            Assert.That(actual, Is.EqualTo(default(TestEnum)));
        }
    }
}
