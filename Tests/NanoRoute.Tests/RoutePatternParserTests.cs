/********************************************************************************
* RoutePatternParserTests.cs                                                    *
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
    internal sealed class RoutePatternParserTests
    {
        private static ValueParserDefinition ParseValue(string definition)
        {
            int offset = 0;
            ValueParserDefinition parsed = ValueParserDefinition.Parse(definition, ref offset);

            Assert.That(offset, Is.EqualTo(definition.Length));
            return parsed;
        }

        [Test]
        public void ParseRoutePattern_ShouldReturnLiteralAndParameterDefinitions()
        {
            object[] definitions = RoutePatternParser.ParseRoutePattern("/items/{id:int(min=1)}/details").ToArray();

            Assert.That(definitions, Has.Length.EqualTo(3));
            Assert.That(definitions[0], Is.InstanceOf<ReadOnlyMemory<char>>());
            Assert.That(definitions[1], Is.InstanceOf<ParameterDefinition>());
            Assert.That(definitions[2], Is.InstanceOf<ReadOnlyMemory<char>>());

            Assert.That(((ReadOnlyMemory<char>) definitions[0]).ToString(), Is.EqualTo("items"));

            ParameterDefinition parameter = (ParameterDefinition) definitions[1];
            Assert.That(parameter.ParameterName, Is.EqualTo("id"));
            Assert.That(parameter.IsOptional, Is.False);
            Assert.That(parameter.ValueParser, Is.EqualTo(ParseValue("int(min=1)")));

            Assert.That(((ReadOnlyMemory<char>) definitions[2]).ToString(), Is.EqualTo("details"));
        }

        [Test]
        public void ParseRoutePattern_ShouldAllowTrailingSeparator()
        {
            object[] definitions = RoutePatternParser.ParseRoutePattern("/items/{id:int}/details/").ToArray();

            Assert.That(definitions, Has.Length.EqualTo(3));
            Assert.That(definitions[0], Is.InstanceOf<ReadOnlyMemory<char>>());
            Assert.That(definitions[1], Is.InstanceOf<ParameterDefinition>());
            Assert.That(definitions[2], Is.InstanceOf<ReadOnlyMemory<char>>());

            Assert.That(((ReadOnlyMemory<char>) definitions[0]).ToString(), Is.EqualTo("items"));
            Assert.That(((ParameterDefinition) definitions[1]).ParameterName, Is.EqualTo("id"));
            Assert.That(((ReadOnlyMemory<char>) definitions[2]).ToString(), Is.EqualTo("details"));
        }

        [Test]
        public void ParseRoutePattern_ShouldIgnoreSeparatorsInsideParserArguments()
        {
            object[] definitions = RoutePatternParser.ParseRoutePattern("/{value:str(pattern='/')}/cica").ToArray();

            Assert.That(definitions, Has.Length.EqualTo(2));
            Assert.That(definitions[0], Is.InstanceOf<ParameterDefinition>());
            Assert.That(definitions[1], Is.InstanceOf<ReadOnlyMemory<char>>());

            ParameterDefinition parameter = (ParameterDefinition) definitions[0];
            Assert.That(parameter.ParameterName, Is.EqualTo("value"));
            Assert.That(parameter.ValueParser, Is.EqualTo(ParseValue("str(pattern='/')")));
            Assert.That(((ReadOnlyMemory<char>) definitions[1]).ToString(), Is.EqualTo("cica"));
        }

        [TestCase("")]
        [TestCase("/")]
        public void ParseRoutePattern_ShouldReturnEmptyDefinitionsForEmptyRoutes(string pattern)
        {
            Assert.That(RoutePatternParser.ParseRoutePattern(pattern).ToArray(), Is.Empty);
        }

        [TestCase("items")]
        [TestCase("items/{id:int}")]
        public void ParseRoutePattern_ShouldRejectPatternsWithoutLeadingSeparator(string pattern)
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => RoutePatternParser.ParseRoutePattern(pattern).ToArray())!;

            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, 0)));
        }

        [TestCase("//", 1)]
        [TestCase("/items//details", 7)]
        [TestCase("/items/{id:int}//details", 16)]
        public void ParseRoutePattern_ShouldRejectRepeatedSeparators(string pattern, int expectedOffset)
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => RoutePatternParser.ParseRoutePattern(pattern).ToArray())!;

            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, expectedOffset)));
        }

        [TestCase("/items/bad[segment]", 10)]
        [TestCase("/items/{id:int}tail", 15)]
        public void ParseRoutePattern_ShouldRejectInvalidSeparatorsAfterDefinitions(string pattern, int expectedOffset)
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => RoutePatternParser.ParseRoutePattern(pattern).ToArray())!;

            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, expectedOffset)));
        }

        [Test]
        public void ParseRoutePattern_ShouldRejectOptionalParameters()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => RoutePatternParser.ParseRoutePattern("/items/{id?:int}").ToArray())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_OPTIONAL_PARAMETERS_NOT_SUPPORTED));
        }

        [Test]
        public void ParseQueryPattern_ShouldReturnParameterDefinitions()
        {
            ParameterDefinition[] definitions = RoutePatternParser.ParseQueryPattern("{filter:str(pattern='[a-z]+')}&{page?:int(min=1)}").ToArray();

            Assert.That(definitions, Has.Length.EqualTo(2));

            Assert.That(definitions[0].ParameterName, Is.EqualTo("filter"));
            Assert.That(definitions[0].IsOptional, Is.False);
            Assert.That(definitions[0].ValueParser, Is.EqualTo(ParseValue("str(pattern='[a-z]+')")));

            Assert.That(definitions[1].ParameterName, Is.EqualTo("page"));
            Assert.That(definitions[1].IsOptional, Is.True);
            Assert.That(definitions[1].ValueParser, Is.EqualTo(ParseValue("int(min=1)")));
        }

        [Test]
        public void ParseQueryPattern_ShouldIgnoreSeparatorsInsideParserArguments()
        {
            ParameterDefinition[] definitions = RoutePatternParser.ParseQueryPattern("{filter:str(pattern='a&b')}&{page:int}").ToArray();

            Assert.That(definitions, Has.Length.EqualTo(2));

            Assert.That(definitions[0].ParameterName, Is.EqualTo("filter"));
            Assert.That(definitions[0].ValueParser, Is.EqualTo(ParseValue("str(pattern='a&b')")));

            Assert.That(definitions[1].ParameterName, Is.EqualTo("page"));
            Assert.That(definitions[1].ValueParser, Is.EqualTo(ParseValue("int")));
        }

        [TestCase("", 0)]
        [TestCase("{filter:str}&", 13)]
        [TestCase("{filter:str}{page:int}", 12)]
        [TestCase("{filter:str}/{page:int}", 12)]
        public void ParseQueryPattern_ShouldRejectInvalidQueryPatternStructure(string pattern, int expectedOffset)
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => RoutePatternParser.ParseQueryPattern(pattern).ToArray())!;

            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, expectedOffset)));
        }
    }
}
