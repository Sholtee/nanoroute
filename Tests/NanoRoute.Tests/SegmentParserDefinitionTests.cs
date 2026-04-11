/********************************************************************************
* SegmentParserDefinitionTests.cs                                               *
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
    internal sealed class SegmentParserDefinitionTests
    {
        [Test]
        public void Create_ShouldParseTheFullSegmentDefinition()
        {
            SegmentParserDefinition definition = SegmentParserDefinition.Create("{id:int(min=3,text='it\\'s okay')}");

            Assert.That(definition.ParameterName, Is.EqualTo("id"));
            Assert.That(definition.ValueParser, Is.EqualTo(ValueParserDefinition.Create("int(min=3,text='it\\'s okay')")));
        }

        [TestCase("{id:int}", "id")]
        [TestCase("{id:int()}", "id")]
        [TestCase("{int}", null)]
        [TestCase("{int()}", null)]
        public void Create_ShouldHandleOptionalParameterNamesAndArguments(string segment, string expectedParameterName)
        {
            SegmentParserDefinition definition = SegmentParserDefinition.Create(segment);

            Assert.That(definition.ParameterName, Is.EqualTo(expectedParameterName));
            Assert.That(definition.ValueParser, Is.EqualTo(ValueParserDefinition.Create("int()")));
        }

        [TestCase("{invalid-segment}")]
        [TestCase("{id:invalid-segment}")]
        [TestCase("{9bad:int}")]
        [TestCase("{id:9bad}")]
        [TestCase("literal")]
        [TestCase("part-1")]
        [TestCase("some%20value")]
        public void Create_ShouldThrowOnInvalidSegments(string segment)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => SegmentParserDefinition.Create(segment))!;

            Assert.That(ex.ParamName, Is.EqualTo("definition"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PATTERN));
        }
    }
}
