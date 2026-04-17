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
    using Properties;

    [TestFixture]
    internal sealed class ParameterDefinitionTests
    {
        [Test]
        public void Create_ShouldParseTheFullSegmentDefinition()
        {
            ParameterDefinition definition = ParameterDefinition.Create("{id:int(min=3,text='it\\'s okay')}");

            Assert.That(definition.ParameterName, Is.EqualTo("id"));
            Assert.That(definition.IsOptional, Is.False);
            Assert.That(definition.ValueParser, Is.EqualTo(ValueParserDefinition.Create("int(min=3,text='it\\'s okay')")));
        }

        [TestCase("{id:int}", "id", false)]
        [TestCase("{id?:int}", "id", true)]
        [TestCase("{id:int()}", "id", false)]
        [TestCase("{int}", null, false)]
        [TestCase("{int()}", null, false)]
        public void Create_ShouldHandleOptionalParameterNamesAndArguments(string segment, string expectedParameterName, bool expectedOptional)
        {
            ParameterDefinition definition = ParameterDefinition.Create(segment);

            Assert.That(definition.ParameterName, Is.EqualTo(expectedParameterName));
            Assert.That(definition.IsOptional, Is.EqualTo(expectedOptional));
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
            ArgumentException ex = Assert.Throws<ArgumentException>(() => ParameterDefinition.Create(segment))!;

            Assert.That(ex.ParamName, Is.EqualTo("definition"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PATTERN));
        }

        [TestCase("id", true)]
        [TestCase("id?", true)]
        [TestCase("_value123?", true)]
        [TestCase("9bad?", false)]
        [TestCase("invalid-name?", false)]
        [TestCase("id??", false)]
        public void IsValidParameterName_ShouldAcceptTrailingQuestionMarkOnlyWhenTheBaseIdentifierIsValid(string parameterName, bool expected) =>
            Assert.That(ParameterDefinition.IsValidParameterName(parameterName), Is.EqualTo(expected));
    }
}

