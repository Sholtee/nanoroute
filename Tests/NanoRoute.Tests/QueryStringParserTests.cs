/********************************************************************************
* QueryStringParserTests.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Frozen;
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
        private sealed record TestParser(ValueParserDelegate Parse, object? Arguments);

        private static TestParser CreateParser(ValueParserDelegate parse, object? arguments = null) =>
            new(parse, arguments);

        private static Dictionary<ReadOnlyMemory<char>, ParameterParser> CreateExpectedParameters(params (string Name, bool Optional, TestParser Parser)[] parameters) =>
            CreateExpectedParameters(isList: false, parameters);

        private static Dictionary<ReadOnlyMemory<char>, ParameterParser> CreateExpectedParameters(bool isList, params (string Name, bool Optional, TestParser Parser)[] parameters)
        {
            Dictionary<ReadOnlyMemory<char>, ParameterParser> result = new(ReadOnlyMemoryCharComparer.Instance);

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterDefinition definition = new()
                {
                    ValueParser = new()
                    {
                        Name = "str",
                        IsList = isList,
                        RawArguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    },
                    ParameterName = parameters[i].Name,
                    IsOptional = parameters[i].Optional,
                    Index = i
                };

                result.Add(definition.ParameterName!.AsMemory(), new ParameterParser(definition, parameters[i].Parser.Parse, parameters[i].Parser.Arguments));
            }

            return result;
        }

        private static RequestContext CreateContext(Dictionary<string, object?> parameters, Uri uri, IServiceProvider services, CancellationToken cancellation = default) => new()
        {
            Parameters = parameters,
            Services = services,
            Request = new HttpRequestMessage(HttpMethod.Get, uri),
            RemainingPath = default,
            Cancellation = cancellation
        };

        private static async ValueTask Parse(RequestContext context, IReadOnlyDictionary<ReadOnlyMemory<char>, ParameterParser> expectedParameters, QueryParsingConfig? config = null)
        {
            using QueryStringParser parser = new(context, expectedParameters.ToFrozenDictionary(ReadOnlyMemoryCharComparer.Instance), config ?? QueryParsingConfig.Default);

            await parser.Parse();
        }

        [Test]
        public async Task Parse_ShouldParseExpectedQueryParameters()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            Mock<IServiceProvider> mockServices = new(MockBehavior.Strict);
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context =>
                    context.Segment.ToString() == "a b" &&
                    context.Services == mockServices.Object &&
                    context.Arguments!.Equals("args") &&
                    context.Cancellation == CancellationToken.None)))
                .Returns((ValueParserContext context) => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())));

            await Parse
            (
                CreateContext(result, new Uri("https://test.test/items?filter=a%20b&ignored=x"), mockServices.Object, CancellationToken.None),
                CreateExpectedParameters(("filter", false, CreateParser(mockParser.Object, "args")))
            );

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result["filter"], Is.EqualTo("a b"));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Once);
        }

        [Test]
        public async Task Parse_ShouldContinueAfterAsyncParserCompletes()
        {
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);
            int step = 0;

            await Parse
            (
                CreateContext(result, new Uri("https://test.test/items?first=one&second=two"), new Mock<IServiceProvider>(MockBehavior.Strict).Object),
                CreateExpectedParameters
                (
                    ("first", false, CreateParser(async context =>
                    {
                        Assert.That(step, Is.EqualTo(0));
                        step = 1;

                        await Task.Yield();

                        Assert.That(step, Is.EqualTo(1));
                        step = 2;

                        return new ValueParseResult(true, context.Segment.ToString());
                    })),
                    ("second", false, CreateParser(context =>
                    {
                        Assert.That(step, Is.EqualTo(2));
                        step = 3;

                        return new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString()));
                    }))
                )
            );

            Assert.That(result["first"], Is.EqualTo("one"));
            Assert.That(result["second"], Is.EqualTo("two"));
            Assert.That(step, Is.EqualTo(3));
        }

        [Test]
        public async Task Parse_ShouldDecodeQueryValuesAsFormData()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "a b c")))
                .Returns((ValueParserContext context) => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())));

            await Parse
            (
                CreateContext(result, new Uri("https://test.test/items?filter=a+b%20c"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("filter", false, CreateParser(mockParser.Object)))
            );

            Assert.That(result["filter"], Is.EqualTo("a b c"));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Once);
        }

        [Test]
        public async Task Parse_ShouldIgnoreUriFragment()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "abc")))
                .Returns((ValueParserContext context) => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())));

            await Parse
            (
                CreateContext(result, new Uri("https://test.test/items?filter=abc#filter=bad"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("filter", false, CreateParser(mockParser.Object)))
            );

            Assert.That(result["filter"], Is.EqualTo("abc"));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Once);
        }

        [Test]
        public void Parse_ShouldRejectDuplicateDeclaredQueryKeys()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "foo")))
                .Returns(new ValueTask<ValueParseResult>(new ValueParseResult(true, "foo")));

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => Parse
            (
                CreateContext(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), new Uri("https://test.test/items?filter=foo&filter=bar"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("filter", false, CreateParser(mockParser.Object)))
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.EquivalentTo(new List<string> { string.Format(Resources.Culture, Resources.ERR_QUERY_DUPLICATE_PARAMETER, "filter") }));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Once);
        }

        [Test]
        public async Task Parse_ShouldCollectRepeatedDeclaredQueryKeysForListParsers()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);

            mockParser
                .Setup(parser => parser.Invoke(It.IsAny<ValueParserContext>()))
                .Returns((ValueParserContext context) => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())));

            await Parse
            (
                CreateContext(result, new Uri("https://test.test/items?filter=foo&filter=bar&filter=baz"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(true, ("filter", false, CreateParser(mockParser.Object)))
            );

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result["filter"], Is.EquivalentTo(new List<object?> { "foo", "bar", "baz" }));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Exactly(3));
        }

        [Test]
        public async Task Parse_ShouldStoreSingleDeclaredQueryKeyAsListForListParsers()
        {
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);

            await Parse
            (
                CreateContext(result, new Uri("https://test.test/items?filter=foo"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(true, ("filter", false, CreateParser(context => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())))))
            );

            Assert.That(result["filter"], Is.EquivalentTo(new List<object?> { "foo" }));
        }

        [Test]
        public async Task Parse_ShouldReplaceExistingNonListValueForListParsers()
        {
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase)
            {
                ["filter"] = "previous"
            };

            await Parse
            (
                CreateContext(result, new Uri("https://test.test/items?filter=foo"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(true, ("filter", false, CreateParser(context => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())))))
            );

            Assert.That(result["filter"], Is.EquivalentTo(new List<object?> { "foo" }));
        }

        [Test]
        public async Task Parse_ShouldReplaceExistingExternalListValueForListParsers()
        {
            List<object?> existing = ["previous"];
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase)
            {
                ["filter"] = existing
            };

            await Parse
            (
                CreateContext(result, new Uri("https://test.test/items?filter=foo&filter=bar"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(true, ("filter", false, CreateParser(context => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())))))
            );

            Assert.That(result["filter"], Is.Not.SameAs(existing));
            Assert.That(result["filter"], Is.EquivalentTo(new List<object?> { "foo", "bar" }));
            Assert.That(existing, Is.EquivalentTo(new List<object?> { "previous" }));
        }

        [Test]
        public void Parse_ShouldRejectMissingMandatoryParameters()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => Parse
            (
                CreateContext(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), new Uri("https://test.test/items?ignored=bar"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("filter", false, CreateParser(mockParser.Object)))
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_MISSING_PARAMETER, "filter") }));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Never);
        }

        [Test]
        public async Task Parse_ShouldMatchDecodedQueryParameterNames()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "abc")))
                .Returns(new ValueTask<ValueParseResult>(new ValueParseResult(true, "abc")));

            await Parse
            (
                CreateContext(result, new Uri("https://test.test/items?query%5Ffilter=abc"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("query_filter", false, CreateParser(mockParser.Object)))
            );

            Assert.That(result["query_filter"], Is.EqualTo("abc"));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Once);
        }

        [Test]
        public async Task Parse_ShouldTreatEmptyValuesAsEmptyStrings()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == string.Empty)))
                .Returns(new ValueTask<ValueParseResult>(new ValueParseResult(true, string.Empty)));

            await Parse
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

            await Parse
            (
                CreateContext(result, new Uri("https://test.test/items"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("optional", true, CreateParser(mockParser.Object)))
            );

            Assert.That(result, Is.Empty);
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Never);
        }

        [Test]
        public void Parse_ShouldRejectUnexpectedParametersWhenConfigured()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);

            mockParser
                .Setup(parser => parser.Invoke(It.Is<ValueParserContext>(context => context.Segment.ToString() == "abc")))
                .Returns(new ValueTask<ValueParseResult>(new ValueParseResult(true, "abc")));

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => Parse
            (
                CreateContext(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), new Uri("https://test.test/items?filter=abc&unexpected=value"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("filter", false, CreateParser(mockParser.Object))),
                new QueryParsingConfig
                {
                    UnexpectedParameterBehavior = UnexpectedParameterBehavior.Reject
                }
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_UNEXPECTED_PARAMETER, "unexpected") }));
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Once);
        }

        [Test]
        public void QueryParsingConfig_ShouldRejectUnknownUnexpectedParameterBehavior()
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => new QueryParsingConfig
            {
                UnexpectedParameterBehavior = (UnexpectedParameterBehavior) 42
            })!;

            Assert.That(ex.ParamName, Is.EqualTo("value"));
        }

        [Test]
        public void Parse_ShouldRejectMissingQueryParameterNames()
        {
            Mock<ValueParserDelegate> mockParser = new(MockBehavior.Strict);

            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => Parse
            (
                CreateContext(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), new Uri("https://test.test/items?=abc"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("filter", true, CreateParser(mockParser.Object)))
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.Null);
            mockParser.Verify(parser => parser.Invoke(It.IsAny<ValueParserContext>()), Times.Never);
        }

        [Test]
        public void Parse_ShouldRejectInvalidQueryValues()
        {
            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => Parse
            (
                CreateContext(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), new Uri("https://test.test/items?filter=abc"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("filter", false, CreateParser(static _ => new ValueTask<ValueParseResult>(new ValueParseResult(false, null)))))
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.EquivalentTo(new[] { string.Format(Resources.Culture, Resources.ERR_QUERY_INVALID_PARAMETER, "filter") }));
        }

        [Test]
        public void Parse_ShouldRejectInvalidEscapesInQueryValues()
        {
            HttpRequestException ex = Assert.ThrowsAsync<HttpRequestException>(() => Parse
            (
                CreateContext(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), new Uri("https://test.test/items?filter=%GG"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, CancellationToken.None),
                CreateExpectedParameters(("filter", false, CreateParser(static _ => new ValueTask<ValueParseResult>(new ValueParseResult(true, null)))))
            ).AsTask())!;

            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_BAD_REQUEST));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.StatusName], Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Data[NanoRouteExceptionExtensions.ErrorsName], Is.EquivalentTo(new[] { Resources.ERR_DECODING_FAILED }));
        }
    }
}
