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
        private static SegmentParser CreateParser(SegmentParserDelegate parse, object? arguments = null) =>
            new(SegmentParserDefinition.Create("{str}"), parse, arguments);

        private static Dictionary<string, QueryParameterDefinition> CreateExpectedParameters(params QueryParameterDefinition[] parameters)
        {
            Dictionary<string, QueryParameterDefinition> result = new(StringComparer.OrdinalIgnoreCase);

            foreach (QueryParameterDefinition parameter in parameters)
                result.Add(parameter.Name, parameter);

            return result;
        }

        [Test]
        public async Task Parse_ShouldParseExpectedQueryParameters()
        {
            Mock<SegmentParserDelegate> mockParser = new(MockBehavior.Strict);
            Mock<IServiceProvider> mockServices = new(MockBehavior.Strict);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<SegmentParserContext>(context =>
                    context.Segment.ToString() == "a%20b" &&
                    context.Services == mockServices.Object &&
                    context.Arguments!.Equals("args") &&
                    context.Cancellation == CancellationToken.None)))
                .Returns((SegmentParserContext context) => new ValueTask<SegmentParseResult>(new SegmentParseResult(true, context.DecodedSegment.ToString())));

            Dictionary<string, object?> result = await QueryStringParser.Parse
            (
                new Uri("https://test.test/items?filter=a%20b&ignored=x"),
                CreateExpectedParameters(new QueryParameterDefinition("filter", Optional: false, CreateParser(mockParser.Object, "args"))),
                mockServices.Object,
                CancellationToken.None
            );

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result["filter"], Is.EqualTo("a b"));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<SegmentParserContext>()), Times.Once);
        }

        [Test]
        public void Parse_ShouldRejectDuplicateDeclaredQueryKeys()
        {
            Mock<SegmentParserDelegate> mockParser = new(MockBehavior.Strict);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<SegmentParserContext>(context => context.Segment.ToString() == "foo")))
                .Returns(new ValueTask<SegmentParseResult>(new SegmentParseResult(true, "foo")));

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => QueryStringParser.Parse
            (
                new Uri("https://test.test/items?filter=foo&filter=bar"),
                CreateExpectedParameters(new QueryParameterDefinition("filter", Optional: false, CreateParser(mockParser.Object))),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object,
                CancellationToken.None
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ERRORS_NAME], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_DUPLICATE_PARAMTER, "filter") }));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<SegmentParserContext>()), Times.Once);
        }

        [Test]
        public void Parse_ShouldRejectMissingMandatoryParameters()
        {
            Mock<SegmentParserDelegate> mockParser = new(MockBehavior.Strict);

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => QueryStringParser.Parse
            (
                new Uri("https://test.test/items?ignored=bar"),
                CreateExpectedParameters(new QueryParameterDefinition("filter", Optional: false, CreateParser(mockParser.Object))),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object,
                CancellationToken.None
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ERRORS_NAME], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_MISSING_PARAMETER, "filter") }));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<SegmentParserContext>()), Times.Never);
        }

        [Test]
        public async Task Parse_ShouldTreatEmptyValuesAsEmptyStrings()
        {
            Mock<SegmentParserDelegate> mockParser = new(MockBehavior.Strict);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<SegmentParserContext>(context => context.Segment.ToString() == string.Empty)))
                .Returns(new ValueTask<SegmentParseResult>(new SegmentParseResult(true, string.Empty)));

            Dictionary<string, object?> result = await QueryStringParser.Parse
            (
                new Uri("https://test.test/items?filter="),
                CreateExpectedParameters(new QueryParameterDefinition("filter", Optional: false, CreateParser(mockParser.Object))),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object,
                CancellationToken.None
            );

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result["filter"], Is.EqualTo(string.Empty));
        }

        [Test]
        public async Task Parse_ShouldSkipMissingOptionalParameters()
        {
            Mock<SegmentParserDelegate> mockParser = new(MockBehavior.Strict);

            Dictionary<string, object?> result = await QueryStringParser.Parse
            (
                new Uri("https://test.test/items"),
                CreateExpectedParameters(new QueryParameterDefinition("optional", Optional: true, CreateParser(mockParser.Object))),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object,
                CancellationToken.None
            );

            Assert.That(result, Is.Empty);
            mockParser.Verify(parser => parser.Invoke(It.IsAny<SegmentParserContext>()), Times.Never);
        }

        [Test]
        public void Parse_ShouldRejectMissingQueryParameterNames()
        {
            Mock<SegmentParserDelegate> mockParser = new(MockBehavior.Strict);

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => QueryStringParser.Parse
            (
                new Uri("https://test.test/items?=abc"),
                CreateExpectedParameters(new QueryParameterDefinition("filter", Optional: true, CreateParser(mockParser.Object))),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object,
                CancellationToken.None
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ERRORS_NAME], Is.Null);
            mockParser.Verify(parser => parser.Invoke(It.IsAny<SegmentParserContext>()), Times.Never);
        }

        [Test]
        public void Parse_ShouldRejectInvalidQueryValues()
        {
            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => QueryStringParser.Parse
            (
                new Uri("https://test.test/items?filter=abc"),
                CreateExpectedParameters(new QueryParameterDefinition("filter", Optional: false, CreateParser(static _ => new ValueTask<SegmentParseResult>(new SegmentParseResult(false, null))))),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object,
                CancellationToken.None
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.STATUS_NAME], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ERRORS_NAME], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_INVALID_PARAMETER, "filter") }));
        }
    }
}
