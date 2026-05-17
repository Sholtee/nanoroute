/********************************************************************************
* ParameterDefinitionTests.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class ParameterDefinitionTests
    {
        private static ValueParserDefinition ParseValue(string definition)
        {
            int offset = 0;
            ValueParserDefinition parsed = ValueParserDefinition.Parse(definition, ref offset);

            Assert.That(offset, Is.EqualTo(definition.Length));
            return parsed;
        }

        private static ParameterDefinition Parse(string segment)
        {
            int offset = 0;
            ParameterDefinition definition = ParameterDefinition.Parse(segment, ref offset);

            Assert.That(offset, Is.EqualTo(segment.Length - 1));

            return definition;
        }

        [Test]
        public void TryParse_ShouldParseTheFullSegmentDefinition()
        {
            ParameterDefinition definition = Parse("{id:int(min=3,text='it\\'s okay')}");

            Assert.That(definition.ParameterName, Is.EqualTo("id"));
            Assert.That(definition.IsOptional, Is.False);
            Assert.That(definition.ValueParser, Is.EqualTo(ParseValue("int(min=3,text='it\\'s okay')")));
        }

        [TestCase("{id:int}", "id", false)]
        [TestCase("{9id:int}", "9id", false)]
        [TestCase("{id?:int}", "id", true)]
        [TestCase("{id:int()}", "id", false)]
        [TestCase("{int}", null, false)]
        [TestCase("{int()}", null, false)]
        public void TryParse_ShouldHandleOptionalParameterNamesAndArguments(string segment, string expectedParameterName, bool expectedOptional)
        {
            ParameterDefinition definition = Parse(segment);

            Assert.That(definition.ParameterName, Is.EqualTo(expectedParameterName));
            Assert.That(definition.IsOptional, Is.EqualTo(expectedOptional));
            Assert.That(definition.ValueParser, Is.EqualTo(ParseValue("int()")));
        }

        [TestCase("{invalid-segment}")]
        [TestCase("{?:int}")]
        [TestCase("{:int}")]
        [TestCase("{id:invalid-segment}")]
        [TestCase("{id??:int}")]
        [TestCase("{id:9bad}")]
        [TestCase("literal")]
        [TestCase("part-1")]
        [TestCase("some%20value")]
        public void TryParse_ShouldRejectInvalidSegments(string segment)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => Parse(segment))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith("Invalid pattern"));
        }

        [Test]
        public void TryParse_ShouldConsumeParameterSegmentsContainingQuotedSlash()
        {
            const string PARAMETER_DEF = "/{value:str(pattern='/')}";

            int offset = 1;
            ParameterDefinition definition = ParameterDefinition.Parse($"{PARAMETER_DEF}/cica", ref offset);

            Assert.That(offset, Is.EqualTo(PARAMETER_DEF.Length - 1));
            Assert.That(definition.ParameterName, Is.EqualTo("value"));
            Assert.That(definition.ValueParser, Is.EqualTo(ParseValue("str(pattern='/')")));
        }

        [Test]
        public void Parse_ShouldLeaveOffsetOnTheClosingBrace()
        {
            int offset = 0;
            ParameterDefinition definition = ParameterDefinition.Parse("{id:int}/tail", ref offset);

            Assert.That(offset, Is.EqualTo("{id:int}".Length - 1));
            Assert.That(definition.ParameterName, Is.EqualTo("id"));
        }
    }
}

