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
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("   ")]
        public void ParseArguments_ShouldReturnEmptyDictionaryForMissingArguments(string args)
        {
            IReadOnlyDictionary<string, string> result = ValueParserDefinition.ParseArguments(args);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseArguments_ShouldHandleNamedArgumentsWithWhitespace()
        {
            IReadOnlyDictionary<string, string> result = ValueParserDefinition.ParseArguments(" min = 3 , text = 'hello' , flag = true ");

            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result["min"], Is.EqualTo("3"));
            Assert.That(result["text"], Is.EqualTo("hello"));
            Assert.That(result["flag"], Is.EqualTo("true"));
        }

        [Test]
        public void ParseArguments_ShouldOnlyUnescapeEscapedQuotes()
        {
            IReadOnlyDictionary<string, string> result = ValueParserDefinition.ParseArguments(@"text='it\'s okay',path='a\\b',line='x\ny',pair='cica,mica=kutya'");

            Assert.That(result["text"], Is.EqualTo("it's okay"));
            Assert.That(result["path"], Is.EqualTo(@"a\\b"));
            Assert.That(result["line"], Is.EqualTo(@"x\ny"));
            Assert.That(result["pair"], Is.EqualTo("cica,mica=kutya"));
        }

        [Test]
        public void ParseArguments_ShouldTreatArgumentNamesCaseInsensitively()
        {
            IReadOnlyDictionary<string, string> result = ValueParserDefinition.ParseArguments("MIN=3,max=9");

            Assert.That(result["min"], Is.EqualTo("3"));
            Assert.That(result["MAX"], Is.EqualTo("9"));
        }

        [Test]
        public void ParseArguments_ShouldAcceptSupportedScalarValueKinds()
        {
            IReadOnlyDictionary<string, string> result = ValueParserDefinition.ParseArguments("none=null,flag=false,count=-12,ratio=-0.5,text='hello'");

            Assert.That(result["none"], Is.EqualTo("null"));
            Assert.That(result["flag"], Is.EqualTo("false"));
            Assert.That(result["count"], Is.EqualTo("-12"));
            Assert.That(result["ratio"], Is.EqualTo("-0.5"));
            Assert.That(result["text"], Is.EqualTo("hello"));
        }

        [Test]
        public void Create_ShouldParseTheFullValueDefinition()
        {
            ValueParserDefinition definition = ValueParserDefinition.Create("int(min=3,text='it\\'s okay')");

            Assert.That(definition.Name, Is.EqualTo("int"));
            Assert.That(definition.RawArguments["min"], Is.EqualTo("3"));
            Assert.That(definition.RawArguments["text"], Is.EqualTo("it's okay"));
        }

        [TestCase("int")]
        [TestCase("int()")]
        public void Create_ShouldHandleOptionalArguments(string definitionText)
        {
            ValueParserDefinition definition = ValueParserDefinition.Create(definitionText);

            Assert.That(definition.Name, Is.EqualTo("int"));
            Assert.That(definition.RawArguments, Has.Count.EqualTo(0));
        }

        [TestCase("invalid-segment")]
        [TestCase("9bad")]
        [TestCase("literal value")]
        [TestCase("some%20value")]
        public void Create_ShouldThrowOnInvalidDefinitions(string definitionText)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => ValueParserDefinition.Create(definitionText))!;

            Assert.That(ex.ParamName, Is.EqualTo("definition"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PATTERN));
        }

        [TestCase("=1")]
        [TestCase("min")]
        [TestCase("min=")]
        [TestCase("min=1,")]
        [TestCase("min='oops")]
        [TestCase("min=1,,max=2")]
        [TestCase("min=1 max=2")]
        [TestCase("1min=2")]
        [TestCase("text=hello")]
        [TestCase("flag=True")]
        public void ParseArguments_ShouldRejectMalformedArgumentLists(string args)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => ValueParserDefinition.ParseArguments(args))!;

            Assert.That(ex.ParamName, Is.EqualTo("args"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PARSERS_ARGS));
        }

        [Test]
        public void ParseArguments_ShouldRejectDuplicateArgumentNames()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => ValueParserDefinition.ParseArguments("min=1,min=2"))!;

            Assert.That(ex.ParamName, Is.EqualTo("args"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_DUPLICATE_PARSER_ARGS));
        }

        [TestCase("int(min=3)", "INT(MIN=3)", true)]
        [TestCase("STR(pattern='[a-z]+',text='hello')", "str(PATTERN='[A-Z]+',TEXT='HELLO')", true)]
        [TestCase("int", "INT()", true)]
        [TestCase("int(min=3,max=5)", "int(min=3)", false)]
        [TestCase("str(min=3)", "str(max=3)", false)]
        [TestCase("int(min=3)", "int(min=4)", false)]
        [TestCase("int(min=3)", "str(min=3)", false)]
        public void Equals_ShouldMatchTheExpectedContract(string leftDefinition, string rightDefinition, bool expected)
        {
            ValueParserDefinition
                left = ValueParserDefinition.Create(leftDefinition),
                right = ValueParserDefinition.Create(rightDefinition);

            Assert.That(left.Equals(right), Is.EqualTo(expected));
        }
    }
}
