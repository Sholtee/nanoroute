/********************************************************************************
* ValueParserDefinitionTests.cs                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;
    using Properties;

    [TestFixture]
    internal sealed class ValueParserDefinitionTests
    {
        private static ValueParserDefinition Parse(string definition)
        {
            int offset = 0;
            ValueParserDefinition parsed = ValueParserDefinition.Parse(definition, ref offset);

            Assert.That(offset, Is.EqualTo(definition.Length));
            return parsed;
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("   ")]
        public void Parse_ShouldReturnEmptyDictionaryForMissingArguments(string args)
        {
            IReadOnlyDictionary<string, string> result = Parse($"parser({args})").RawArguments;

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Parse_ShouldHandleNamedArgumentsWithWhitespace()
        {
            IReadOnlyDictionary<string, string> result = Parse("parser( min = 3 , text = 'hello' , flag = true )").RawArguments;

            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result["min"], Is.EqualTo("3"));
            Assert.That(result["text"], Is.EqualTo("hello"));
            Assert.That(result["flag"], Is.EqualTo("true"));
        }

        [Test]
        public void Parse_ShouldOnlyUnescapeEscapedQuotes()
        {
            IReadOnlyDictionary<string, string> result = Parse(@"parser(text='it\'s okay',path='a\\b',line='x\ny',pair='cica,mica=kutya')").RawArguments;

            Assert.That(result["text"], Is.EqualTo("it's okay"));
            Assert.That(result["path"], Is.EqualTo(@"a\\b"));
            Assert.That(result["line"], Is.EqualTo(@"x\ny"));
            Assert.That(result["pair"], Is.EqualTo("cica,mica=kutya"));
        }

        [Test]
        public void Parse_ShouldTreatArgumentNamesCaseInsensitively()
        {
            IReadOnlyDictionary<string, string> result = Parse("parser(MIN=3,max=9)").RawArguments;

            Assert.That(result["min"], Is.EqualTo("3"));
            Assert.That(result["MAX"], Is.EqualTo("9"));
        }

        [Test]
        public void Parse_ShouldAcceptSupportedScalarValueKinds()
        {
            IReadOnlyDictionary<string, string> result = Parse("parser(none=null,flag=false,count=-12,ratio=-0.5,text='hello')").RawArguments;

            Assert.That(result["none"], Is.EqualTo("null"));
            Assert.That(result["flag"], Is.EqualTo("false"));
            Assert.That(result["count"], Is.EqualTo("-12"));
            Assert.That(result["ratio"], Is.EqualTo("-0.5"));
            Assert.That(result["text"], Is.EqualTo("hello"));
        }

        [Test]
        public void Parse_ShouldParseTheFullValueDefinition()
        {
            ValueParserDefinition definition = Parse("int(min=3,text='it\\'s okay')");

            Assert.That(definition.Name, Is.EqualTo("int"));
            Assert.That(definition.IsList, Is.False);
            Assert.That(definition.RawArguments["min"], Is.EqualTo("3"));
            Assert.That(definition.RawArguments["text"], Is.EqualTo("it's okay"));
        }

        [Test]
        public void Parse_ShouldParseListValueDefinition()
        {
            ValueParserDefinition definition = Parse("int[](min=3)");

            Assert.That(definition.Name, Is.EqualTo("int"));
            Assert.That(definition.IsList, Is.True);
            Assert.That(definition.RawArguments["min"], Is.EqualTo("3"));
        }

        [TestCase("int")]
        [TestCase("int()")]
        public void Parse_ShouldHandleOptionalArguments(string definitionText)
        {
            ValueParserDefinition definition = Parse(definitionText);

            Assert.That(definition.Name, Is.EqualTo("int"));
            Assert.That(definition.IsList, Is.False);
            Assert.That(definition.RawArguments, Has.Count.EqualTo(0));
        }

        [TestCase("9bad")]
        public void Parse_ShouldRejectInvalidDefinitions(string definitionText)
        {
            int offset = 0;
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => ValueParserDefinition.Parse(definitionText, ref offset))!;

            Assert.That(ex.Message, Does.StartWith("Invalid pattern"));
        }

        [TestCase("invalid-segment", "invalid")]
        [TestCase("literal value", "literal")]
        [TestCase("some%20value", "some")]
        [TestCase("parser(=1)", "parser")]
        [TestCase("parser(min)", "parser")]
        [TestCase("parser(min=)", "parser")]
        [TestCase("parser(min=1,)", "parser")]
        [TestCase("parser(min='oops)", "parser")]
        [TestCase("parser(min=1,,max=2)", "parser")]
        [TestCase("parser(min=1 max=2)", "parser")]
        [TestCase("parser(1min=2)", "parser")]
        [TestCase("parser(text=hello)", "parser")]
        [TestCase("parser(flag=True)", "parser")]
        public void Parse_ShouldStopBeforeInvalidTail(string definitionText, string expectedName)
        {
            int offset = 0;
            ValueParserDefinition definition = ValueParserDefinition.Parse(definitionText, ref offset);

            Assert.That(definition.Name, Is.EqualTo(expectedName));
            Assert.That(offset, Is.EqualTo(expectedName.Length));
        }

        [Test]
        public void Parse_ShouldRejectDuplicateArgumentNames()
        {
            int offset = 0;
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => ValueParserDefinition.Parse("parser(min=1,min=2)", ref offset))!;

            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_INVALID_ARGUMENTS, "parser", 0)));
        }

        [TestCase("int(min=3)", "INT(MIN=3)", true)]
        [TestCase("int[](min=3)", "INT[](MIN=3)", true)]
        [TestCase("STR(pattern='[a-z]+',text='hello')", "str(PATTERN='[A-Z]+',TEXT='HELLO')", true)]
        [TestCase("int", "INT()", true)]
        [TestCase("int[]", "int", false)]
        [TestCase("int(min=3,max=5)", "int(min=3)", false)]
        [TestCase("str(min=3)", "str(max=3)", false)]
        [TestCase("int(min=3)", "int(min=4)", false)]
        [TestCase("int(min=3)", "str(min=3)", false)]
        public void Equals_ShouldMatchTheExpectedContract(string leftDefinition, string rightDefinition, bool expected)
        {
            ValueParserDefinition
                left = Parse(leftDefinition),
                right = Parse(rightDefinition);

            Assert.That(left.Equals(right), Is.EqualTo(expected));
        }

        [Test]
        public void Equals_ShouldReturnFalseForOtherObjectTypes()
        {
            Assert.That(Parse("int").Equals("int"), Is.False);
        }

        [Test]
        public void GetHashCode_ShouldThrow()
        {
            Assert.Throws<NotImplementedException>(() => Parse("int").GetHashCode());
        }
    }
}
