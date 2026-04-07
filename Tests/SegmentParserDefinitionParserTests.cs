/********************************************************************************
* SegmentParserDefinitionParserTests.cs                                         *
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
    internal sealed class SegmentParserDefinitionParserTests
    {
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("   ")]
        public void ParseArguments_ShouldReturnEmptyDictionaryForMissingArguments(string args)
        {
            IReadOnlyDictionary<string, string> result = SegmentParserDefinitionParser.ParseArguments(args);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseArguments_ShouldHandleNamedArgumentsWithWhitespace()
        {
            IReadOnlyDictionary<string, string> result = SegmentParserDefinitionParser.ParseArguments(" min = 3 , text = 'hello' , flag = true ");

            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result["min"], Is.EqualTo("3"));
            Assert.That(result["text"], Is.EqualTo("hello"));
            Assert.That(result["flag"], Is.EqualTo("true"));
        }

        [Test]
        public void ParseArguments_ShouldOnlyUnescapeEscapedQuotes()
        {
            IReadOnlyDictionary<string, string> result = SegmentParserDefinitionParser.ParseArguments(@"text='it\'s okay',path='a\\b',line='x\ny',pair='cica,mica=kutya'");

            Assert.That(result["text"], Is.EqualTo("it's okay"));
            Assert.That(result["path"], Is.EqualTo(@"a\\b"));
            Assert.That(result["line"], Is.EqualTo(@"x\ny"));
            Assert.That(result["pair"], Is.EqualTo("cica,mica=kutya"));
        }

        [Test]
        public void ParseArguments_ShouldTreatArgumentNamesCaseInsensitively()
        {
            IReadOnlyDictionary<string, string> result = SegmentParserDefinitionParser.ParseArguments("MIN=3,max=9");

            Assert.That(result["min"], Is.EqualTo("3"));
            Assert.That(result["MAX"], Is.EqualTo("9"));
        }

        [Test]
        public void ParseArguments_ShouldAcceptSupportedScalarValueKinds()
        {
            IReadOnlyDictionary<string, string> result = SegmentParserDefinitionParser.ParseArguments("none=null,flag=false,count=-12,ratio=-0.5,text='hello'");

            Assert.That(result["none"], Is.EqualTo("null"));
            Assert.That(result["flag"], Is.EqualTo("false"));
            Assert.That(result["count"], Is.EqualTo("-12"));
            Assert.That(result["ratio"], Is.EqualTo("-0.5"));
            Assert.That(result["text"], Is.EqualTo("hello"));
        }

        [Test]
        public void GetSegmentParserDefinition_ShouldParseTheFullSegmentDefinition()
        {
            SegmentParserDefinition definition = SegmentParserDefinitionParser.GetSegmentParserDefinition("{id:int(min=3,text='it\\'s okay')}");

            Assert.That(definition.ParameterName, Is.EqualTo("id"));
            Assert.That(definition.ParserName, Is.EqualTo("int"));
            Assert.That(definition.RawArguments["min"], Is.EqualTo("3"));
            Assert.That(definition.RawArguments["text"], Is.EqualTo("it's okay"));
        }

        [TestCase("{id:int}", "id")]
        [TestCase("{id:int()}", "id")]
        [TestCase("{int}", "")]
        [TestCase("{int()}", "")]
        public void GetSegmentParserDefinition_ShouldHandleOptionalParameterNamesAndArguments(string segment, string expectedParameterName)
        {
            SegmentParserDefinition definition = SegmentParserDefinitionParser.GetSegmentParserDefinition(segment);

            Assert.That(definition.ParameterName, Is.EqualTo(expectedParameterName));
            Assert.That(definition.ParserName, Is.EqualTo("int"));
            Assert.That(definition.RawArguments, Has.Count.EqualTo(0));
        }

        [TestCase("{invalid-segment}")]
        [TestCase("{id:invalid-segment}")]
        [TestCase("{9bad:int}")]
        [TestCase("{id:9bad}")]
        [TestCase("literal")]
        [TestCase("part-1")]
        [TestCase("some%20value")]
        public void GetSegmentParserDefinition_ShouldThrowOnInvalidSegments(string segment)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => SegmentParserDefinitionParser.GetSegmentParserDefinition(segment))!;

            Assert.That(ex.ParamName, Is.EqualTo("segment"));
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
            ArgumentException ex = Assert.Throws<ArgumentException>(() => SegmentParserDefinitionParser.ParseArguments(args))!;

            Assert.That(ex.ParamName, Is.EqualTo("args"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PARSERS_ARGS));
        }

        [Test]
        public void ParseArguments_ShouldRejectDuplicateArgumentNames()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => SegmentParserDefinitionParser.ParseArguments("min=1,min=2"))!;

            Assert.That(ex.ParamName, Is.EqualTo("args"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_DUPLICATE_PARSER_ARGS));
        }
    }
}
