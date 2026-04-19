/********************************************************************************
* QueryStringParserTests.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;
    using Properties;

    [TestFixture]
    internal sealed class QueryStringParserTests
    {
        private static ValueParserDefinition ParseValue(string definition)
        {
            int offset = 0;
            ValueParserDefinition parsed = ValueParserDefinition.Parse(definition, ref offset);

            Assert.That(offset, Is.EqualTo(definition.Length));
            return parsed;
        }

        private static ValueParser CreateParser(ValueParserDelegate parse, object? arguments = null) =>
            new(ParseValue("str"), parse, arguments);

        private static Dictionary<string, QueryParameterDefinition> CreateExpectedParameters(params (string Name, bool Optional, ValueParser Parser)[] parameters)
        {
            Dictionary<string, QueryParameterDefinition> result = new(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < parameters.Length; i++)
            {
                QueryParameterDefinition parameter = new(parameters[i].Name, i, parameters[i].Optional, parameters[i].Parser);
                result.Add(parameter.Name, parameter);
            }

            return result;
        }

        private static RequestContext CreateContext(Dictionary<string, object?> parameters, Uri uri, IServiceProvider services, CancellationToken cancellation = default) =>
            new(parameters, services, new HttpRequestMessage(HttpMethod.Get, uri), cancellation);

        [Test]
        public async Task Parse_ShouldParseExpectedQueryParameters()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            Mock<IServiceProvider> mockServices = new(MockBehavior.Strict);
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context =>
                    context.Segment.ToString() == "a%20b" &&
                    context.Services == mockServices.Object &&
                    context.Arguments!.Equals("args") &&
                    context.Cancellation == CancellationToken.None)))
                .Returns((ValueParserContext context) => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.DecodedSegment.ToString())));

            await QueryStringParser.Parse
            (
                CreateContext(result, new Uri("https://test.test/items?filter=a%20b&ignored=x"), mockServices.Object, CancellationToken.None),
                CreateExpectedParameters(("filter", false, CreateParser(mockParser.Object, "args")))
            );

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result["filter"], Is.EqualTo("a b"));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Once);
        }

        [Test]
        public void Parse_ShouldRejectDuplicateDeclaredQueryKeys()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "foo")))
                .Returns(new ValueTask<ValueParseResult>(new ValueParseResult(true, "foo")));

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => QueryStringParser.Parse
            (
                CreateContext(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), new Uri("https://test.test/items?filter=foo&filter=bar"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("filter", false, CreateParser(mockParser.Object)))
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ERRORS_NAME], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_DUPLICATE_PARAMTER, "filter") }));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Once);
        }

        [Test]
        public void Parse_ShouldRejectMissingMandatoryParameters()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => QueryStringParser.Parse
            (
                CreateContext(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), new Uri("https://test.test/items?ignored=bar"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("filter", false, CreateParser(mockParser.Object)))
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ERRORS_NAME], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_MISSING_PARAMETER, "filter") }));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Never);
        }

        [Test]
        public async Task Parse_ShouldTreatEmptyValuesAsEmptyStrings()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == string.Empty)))
                .Returns(new ValueTask<ValueParseResult>(new ValueParseResult(true, string.Empty)));

            await QueryStringParser.Parse
            (
                CreateContext(result, new Uri("https://test.test/items?filter="), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("filter", false, CreateParser(mockParser.Object)))
            );

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result["filter"], Is.EqualTo(string.Empty));
        }

        [Test]
        public async Task Parse_ShouldSkipMissingOptionalParameters()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);

            await QueryStringParser.Parse
            (
                CreateContext(result, new Uri("https://test.test/items"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("optional", true, CreateParser(mockParser.Object)))
            );

            Assert.That(result, Is.Empty);
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Never);
        }

        [Test]
        public void Parse_ShouldRejectMissingQueryParameterNames()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => QueryStringParser.Parse
            (
                CreateContext(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), new Uri("https://test.test/items?=abc"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("filter", true, CreateParser(mockParser.Object)))
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ERRORS_NAME], Is.Null);
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Never);
        }

        [Test]
        public void Parse_ShouldRejectInvalidQueryValues()
        {
            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => QueryStringParser.Parse
            (
                CreateContext(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), new Uri("https://test.test/items?filter=abc"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("filter", false, CreateParser(static _ => new ValueTask<ValueParseResult>(new ValueParseResult(false, null)))))
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ERRORS_NAME], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_INVALID_PARAMETER, "filter") }));
        }
    }
}

