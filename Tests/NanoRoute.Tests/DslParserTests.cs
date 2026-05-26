/********************************************************************************
* DslParserTests.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;
    using Properties;

    [TestFixture]
    internal sealed class DslParserTests
    {
        private static ValueParserDefinition ParseValue(string definition)
        {
            int offset = 0;
            ValueParserDefinition parsed = ValueParserDefinition.Parse(definition, ref offset);

            Assert.That(offset, Is.EqualTo(definition.Length));
            return parsed;
        }

        private static ReadOnlyMemory<char> AssertLiteralDefinition(object definition)
        {
            Assert.That(definition, Is.InstanceOf<ReadOnlyMemory<char>>());
            return (ReadOnlyMemory<char>) definition;
        }

        private static ParameterDefinition AssertParameterDefinition(object definition)
        {
            Assert.That(definition, Is.InstanceOf<ParameterDefinition>());
            return (ParameterDefinition) definition;
        }

        [Test]
        public void ParseRoutePattern_ShouldReturnLiteralAndParameterDefinitions()
        {
            object[] definitions = DslParser.ParseRoutePattern("/items/{id:int(min=1)}/details/").ToArray();

            Assert.That(definitions, Has.Length.EqualTo(3));
            Assert.That(AssertLiteralDefinition(definitions[0]).ToString(), Is.EqualTo("items"));

            ParameterDefinition parameter = AssertParameterDefinition(definitions[1]);
            Assert.That(parameter.ParameterName, Is.EqualTo("id"));
            Assert.That(parameter.IsOptional, Is.False);
            Assert.That(parameter.ValueParser, Is.EqualTo(ParseValue("int(min=1)")));

            Assert.That(AssertLiteralDefinition(definitions[2]).ToString(), Is.EqualTo("details"));
        }

        [Test]
        public void ParseRoutePattern_ShouldRejectListValueParserDefinitions()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => DslParser.ParseRoutePattern("/items/{ids:int(min=1)[]}").ToArray())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_LIST_PARSERS_NOT_SUPPORTED));
        }

        [Test]
        public void ParseRoutePattern_ShouldIgnoreSeparatorsInsideParserArguments()
        {
            object[] definitions = DslParser.ParseRoutePattern("/{value:regex(pattern='/',timeoutMs=50)}/cica/").ToArray();

            Assert.That(definitions, Has.Length.EqualTo(2));

            ParameterDefinition parameter = AssertParameterDefinition(definitions[0]);
            Assert.That(parameter.ParameterName, Is.EqualTo("value"));
            Assert.That(parameter.ValueParser, Is.EqualTo(ParseValue("regex(pattern='/',timeoutMs=50)")));
            Assert.That(AssertLiteralDefinition(definitions[1]).ToString(), Is.EqualTo("cica"));
        }

        [TestCase("/*", 0)]
        [TestCase("/items/*", 1)]
        [TestCase("/items/{id:int}/*", 2)]
        public void ParseRoutePattern_ShouldSkipTrailingAsterisks(string pattern, int expectedLength)
        {
            Assert.That(DslParser.ParseRoutePattern(pattern).Count(), Is.EqualTo(expectedLength));
        }

        [Test]
        public void ParseRoutePattern_ShouldReturnEmptyDefinitionsForEmptyRoutes()
        {
            Assert.That(DslParser.ParseRoutePattern("/").ToArray(), Is.Empty);
        }

        [TestCase("")]
        [TestCase("items")]
        [TestCase("items/{id:int}")]
        public void ParseRoutePattern_ShouldRejectPatternsWithoutLeadingSeparator(string pattern)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => DslParser.ParseRoutePattern(pattern).ToArray())!;

            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, 0)));
        }

        [TestCase("*", 0)]
        [TestCase("/*/", 1)]
        [TestCase("/items*/{id:int}/", 6)]
        [TestCase("/items/{id:int}*/", 15)]
        public void ParseRoutePattern_ShouldRejectInvalidAsterisks(string pattern, int expectedOffset)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => DslParser.ParseRoutePattern(pattern).ToArray())!;

            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, expectedOffset)));
        }

        [TestCase("//", 1)]
        [TestCase("/items//details/", 7)]
        [TestCase("/items/{id:int}//details/", 16)]
        public void ParseRoutePattern_ShouldRejectRepeatedSeparators(string pattern, int expectedOffset)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => DslParser.ParseRoutePattern(pattern).ToArray())!;

            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, expectedOffset)));
        }

        [TestCase("/items/bad[segment]", 10)]
        [TestCase("/items?filter=x", 6)]
        [TestCase("/items&filter=x", 6)]
        [TestCase("/items=filter", 6)]
        [TestCase("/items", 6)]
        [TestCase("/items/{id:int}/details", 23)]
        [TestCase("/items/{id:int}tail", 15)]
        public void ParseRoutePattern_ShouldRejectInvalidSeparatorsAfterDefinitions(string pattern, int expectedOffset)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => DslParser.ParseRoutePattern(pattern).ToArray())!;

            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, expectedOffset)));
        }

        [Test]
        public void ParseRoutePattern_ShouldRejectOptionalParameters()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => DslParser.ParseRoutePattern("/items/{id?:int}").ToArray())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_OPTIONAL_PARAMETERS_NOT_SUPPORTED));
        }

        [Test]
        public void ParseQueryPattern_ShouldReturnParameterDefinitions()
        {
            ParameterDefinition[] definitions = DslParser.ParseQueryPattern("{filter:regex(pattern='[a-z]+',timeoutMs=50)}&{page?:int(min=1)}").ToArray();

            Assert.That(definitions, Has.Length.EqualTo(2));

            Assert.That(definitions[0].ParameterName, Is.EqualTo("filter"));
            Assert.That(definitions[0].IsOptional, Is.False);
            Assert.That(definitions[0].ValueParser, Is.EqualTo(ParseValue("regex(pattern='[a-z]+',timeoutMs=50)")));

            Assert.That(definitions[1].ParameterName, Is.EqualTo("page"));
            Assert.That(definitions[1].IsOptional, Is.True);
            Assert.That(definitions[1].ValueParser, Is.EqualTo(ParseValue("int(min=1)")));
        }

        [Test]
        public void ParseQueryPattern_ShouldAllowListValueParserDefinitions()
        {
            ParameterDefinition[] definitions = DslParser.ParseQueryPattern("{ids:int(min=1)[]}").ToArray();

            Assert.That(definitions, Has.Length.EqualTo(1));
            Assert.That(definitions[0].ParameterName, Is.EqualTo("ids"));
            Assert.That(definitions[0].ValueParser.Name, Is.EqualTo("int"));
            Assert.That(definitions[0].ValueParser.IsList, Is.True);
            Assert.That(definitions[0].ValueParser.RawArguments["min"], Is.EqualTo("1"));
        }

        [Test]
        public void ParseQueryPattern_ShouldIgnoreSeparatorsInsideParserArguments()
        {
            ParameterDefinition[] definitions = DslParser.ParseQueryPattern("{filter:regex(pattern='a&b',timeoutMs=50)}&{page:int}").ToArray();

            Assert.That(definitions, Has.Length.EqualTo(2));

            Assert.That(definitions[0].ParameterName, Is.EqualTo("filter"));
            Assert.That(definitions[0].ValueParser, Is.EqualTo(ParseValue("regex(pattern='a&b',timeoutMs=50)")));

            Assert.That(definitions[1].ParameterName, Is.EqualTo("page"));
            Assert.That(definitions[1].ValueParser, Is.EqualTo(ParseValue("int")));
        }

        [TestCase("{filter:str}&", 13)]
        [TestCase("{filter:str}{page:int}", 12)]
        [TestCase("{filter:str}/{page:int}", 12)]
        public void ParseQueryPattern_ShouldRejectInvalidQueryPatternStructure(string pattern, int expectedOffset)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => DslParser.ParseQueryPattern(pattern).ToArray())!;

            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, expectedOffset)));
        }
    }
}
