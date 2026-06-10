/********************************************************************************
* RouterBuilderTests.ValueParsers.cs                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;
    using Properties;

    internal sealed partial class RouterBuilderTests
    {
        [Test]
        public void WithBase_ShouldInheritTheParentValueParsers()
        {
            RouteScopeBuilder childBuilder = _routerBuilder
                .AddValueParser("str", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = segment.ToString(); return true; })
                .CreatePrefix("/to/*")
                .AddValueParser("int", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = segment.ToString(); return true; });

            Assert.DoesNotThrow(() => childBuilder.AddHandler("/{str}/{int}/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object));
        }

        [Test]
        public void WithBase_ShouldCreateAnIndependentParserScope()
        {
            _routerBuilder
                .AddValueParser("str", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = segment.ToString(); return true; })
                .CreatePrefix("/to/*")
                .AddValueParser("int", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = segment.ToString(); return true; });

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _routerBuilder.AddHandler("/{str}/{int}/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_NO_SUCH_PARSER, "int")));
        }

        [Test]
        public async Task AddValueParser_ShouldReplaceExistingParserRegistrations()
        {
            HttpMessageRouter router = _routerBuilder
                .AddValueParser("value", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = $"first:{segment}"; return true; })
                .AddValueParser("value", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = $"second:{segment}"; return true; })
                .AddHandler("GET", "/items/{id:value}/", async (context, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(context.Parameters["id"]!.ToString()!) })
                .CreateRouter();

            HttpResponseMessage response = await router.Route(new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = new Uri("https://test.test/items/42") }, s_services);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("second:42"));
        }

        [Test]
        public async Task AddValueParser_ShouldSupportAsyncParserWithoutArguments()
        {
            HttpMessageRouter router = _routerBuilder
                .AddValueParser("value", static context => new ValueTask<ValueParseResult>(new ValueParseResult(true, $"async:{context.Segment}")))
                .AddHandler("GET", "/items/{id:value}/", async (context, _) => new HttpResponseMessage
                {
                    Content = new StringContent((string) context.Parameters["id"]!)
                })
                .CreateRouter();

            HttpResponseMessage response = await router.Route(new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/42"), s_services);

            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("async:42"));
        }

        [Test]
        public async Task AddValueParser_ShouldSupportAsyncParserWithBoundArguments()
        {
            HttpMessageRouter router = _routerBuilder
                .AddValueParser("value", static args => args["prefix"], static context => new ValueTask<ValueParseResult>(new ValueParseResult(true, $"{context.Arguments}:{context.Segment}")))
                .AddHandler("GET", "/items/{id:value(prefix='bound')}/", async (context, _) => new HttpResponseMessage
                {
                    Content = new StringContent((string) context.Parameters["id"]!)
                })
                .CreateRouter();

            HttpResponseMessage response = await router.Route(new HttpRequestMessage(HttpMethod.Get, "https://test.test/items/42"), s_services);

            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("bound:42"));
        }

        [Test]
        public void AddValueParser_ShouldReturnTheOriginalBuilder()
        {
            RouterBuilder<HttpMessageRouter, RouterConfig> result = NanoRouteValueParserExtensions.AddValueParser
            (
                _routerBuilder,
                "value",
                new Mock<BindArgumentsDelegate>(MockBehavior.Strict).Object,
                new Mock<ValueParserDelegate>(MockBehavior.Strict).Object
            );

            Assert.That(result, Is.SameAs(_routerBuilder));
        }

        [Test]
        public async Task WithBase_ShouldKeepChildParserOverridesLocal()
        {
            RouteScopeBuilder childBuilder = _routerBuilder
                .AddValueParser("value", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = $"parent:{segment}"; return true; })
                .CreatePrefix("/child/*")
                .AddValueParser("value", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = $"child:{segment}"; return true; });

            RequestHandlerDelegate handler = async (context, _) => new HttpResponseMessage { Content = new StringContent(context.Parameters["id"]!.ToString()!) };

            childBuilder
                .AddHandler("GET", "/{id:value}/", handler);

            _routerBuilder
                .AddHandler("GET", "/{id:value}/", handler);

            HttpMessageRouter router = _routerBuilder.CreateRouter();

            HttpResponseMessage
                parentResponse = await router.Route(new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = new Uri("https://test.test/42") }, s_services),
                childResponse = await router.Route(new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = new Uri("https://test.test/child/42") }, s_services);

            Assert.That(await parentResponse.Content.ReadAsStringAsync(), Is.EqualTo("parent:42"));
            Assert.That(await childResponse.Content.ReadAsStringAsync(), Is.EqualTo("child:42"));
        }

        [Test]
        public void ValueParsers_ShouldReflectTheCurrentScope()
        {
            RouteScopeBuilder childBuilder = _routerBuilder
                .AddValueParser("str", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = segment.ToString(); return true; })
                .CreatePrefix("/child/*")
                .AddValueParser("int", (ReadOnlyMemory<char> segment, object? _, out object? parsed) => { parsed = segment.ToString(); return true; });

            Assert.That(_routerBuilder.ValueParsers.Keys, Is.EquivalentTo(new[] { "str" }));
            Assert.That(childBuilder.ValueParsers.Keys, Is.EquivalentTo(new[] { "str", "int" }));
            Assert.That(_routerBuilder.ValueParsers["str"].Name, Is.EqualTo("str"));
            Assert.That(childBuilder.ValueParsers["int"].Name, Is.EqualTo("int"));
        }

        [TestCase("/path/{id:int(=1)}", 6)]
        [TestCase("/path/{id:int(min='oops)}", 6)]
        [TestCase("/path/{id:int(min=1,,max=2)}", 6)]
        public void AddHandler_ShouldThrowOnMalformedParserArgumentSyntax(string pattern, int expectedOffset)
        {
            _routerBuilder.AddValueParser("int", args => int.Parse(args["min"], CultureInfo.InvariantCulture), new Mock<ValueParserDelegate>(MockBehavior.Strict).Object);

            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, expectedOffset)));
        }

        [Test]
        public void AddHandler_ShouldAllowMixedLiteralAndParserSegments()
        {
            _routerBuilder.AddValueParser("int", new Mock<ValueParserDelegate>(MockBehavior.Strict).Object);

            Assert.DoesNotThrow(() => _routerBuilder.AddHandler("GET", "/orders/{id:int}/details/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object));
        }

        [TestCase("/ok/bad[segment]", 7)]
        [TestCase("/ok/{id:int}/bad[segment]", 16)]
        public void AddHandler_ShouldThrowWhenAnyLiteralSegmentIsInvalid(string pattern, int expectedOffset)
        {
            _routerBuilder.AddValueParser("int", new Mock<ValueParserDelegate>(MockBehavior.Strict).Object);

            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("pattern"));
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, expectedOffset)));
        }

        [TestCase("/path/to/{missing}/")]
        [TestCase("/path/to/{parameter_name:missing}/")]
        public void AddHandler_ShouldThrowOnMissingValueParser(string pattern)
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _routerBuilder.AddHandler(pattern, new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.Culture, Resources.ERR_NO_SUCH_PARSER, "missing")));
        }

        [Test]
        public void AddHandler_ShouldThrowOnParameterOverride()
        {
            _routerBuilder
                .AddValueParser("int", new Mock<ValueParserDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/path/to/{id:int}/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _routerBuilder.AddHandler("GET", "/path/to/{other_id:int}/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_PARAMETER_OVERRIDE));
        }

        [Test]
        public void AddHandler_ShouldAllowReusingUnnamedParsedSegments()
        {
            _routerBuilder
                .AddValueParser("int", new Mock<ValueParserDelegate>(MockBehavior.Strict).Object)
                .AddHandler("GET", "/path/to/{int}/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object);

            Assert.DoesNotThrow(() => _routerBuilder.AddHandler("POST", "/path/to/{int}/", new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object));
        }

        [Test]
        public void AddHandler_ShouldReuseParsedNodesWhenBoundArgumentsAreValueEqual()
        {
            _routerBuilder.AddIntParser();

            RequestHandlerDelegate handler = new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object;

            _routerBuilder
                .AddHandler("GET", "/items/{id:int(min=1,max=2)}/", handler)
                .AddHandler("POST", "/items/{id:int(max=2,min=1)}/", handler);

            RouteNode root = _routerBuilder.CreateSnapshot();

            Assert.That(root.LiteralBranches["items".AsMemory()].ParsedBranches, Has.Count.EqualTo(1));
            Assert.That(root.LiteralBranches["items".AsMemory()].ParsedBranches[0].Key.Definition.ParameterName, Is.EqualTo("id"));
        }

        private static IEnumerable<TestCaseData> AddDefaultValueParsers_ShouldRegisterTheBuiltInParsers_Cases()
        {
            yield return new TestCaseData("int", 42);
            yield return new TestCaseData("guid", Guid.Parse("4a91f2c0-0e3c-4ec8-9f8c-8d2d2f2c7d1a"));
            yield return new TestCaseData("bool", true);
            yield return new TestCaseData("str", "spikey");
        }

        [TestCaseSource(nameof(AddDefaultValueParsers_ShouldRegisterTheBuiltInParsers_Cases))]
        public async Task AddDefaultValueParsers_ShouldRegisterTheBuiltInParsers(string parserName, object expectedValue)
        {
            HttpMessageRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler("GET", $"/items/{{value:{parserName}}}/", async (context, _) => new HttpResponseMessage { Content = new StringContent(context.Parameters["value"]!.ToString()!) })
                .CreateRouter();

            HttpResponseMessage response = await router.Route(new HttpRequestMessage { RequestUri = new Uri($"https://test.test/items/{expectedValue}") }, s_services);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(expectedValue.ToString()));
        }

        [Test]
        public async Task AddDefaultValueParsers_ShouldRegisterTheRegexParser()
        {
            HttpMessageRouter router = _routerBuilder
                .AddDefaultValueParsers()
                .AddHandler("GET", "/items/{value:regex(pattern='^[a-z]+$')}/", async (context, _) => new HttpResponseMessage { Content = new StringContent((string) context.Parameters["value"]!) })
                .CreateRouter();

            HttpResponseMessage response = await router.Route(new HttpRequestMessage { RequestUri = new Uri("https://test.test/items/SPIKEY") }, s_services);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("SPIKEY"));
        }

        [TestCase("/items/{value:int(min=20,max=10)}/")]
        [TestCase("/items/{value:int(foo=10)}/")]
        [TestCase("/items/{value:int(min='oops')}/")]
        [TestCase("/items/{value:int(max=true)}/")]
        [TestCase("/items/{value:str(min=5,max=3)}/")]
        [TestCase("/items/{value:str(min='oops')}/")]
        [TestCase("/items/{value:str(max=false)}/")]
        [TestCase("/items/{value:str(pattern='[')}/")]
        [TestCase("/items/{value:str(foo='bar')}/")]
        [TestCase("/items/{value:regex}/")]
        [TestCase("/items/{value:regex(timeoutMs=50)}/")]
        [TestCase("/items/{value:regex(pattern='[',timeoutMs=50)}/")]
        [TestCase("/items/{value:regex(pattern='^[a-z]+$',timeoutMs=0)}/")]
        [TestCase("/items/{value:regex(pattern='^[a-z]+$',timeoutMs=-1)}/")]
        [TestCase("/items/{value:regex(pattern='^[a-z]+$',timeoutMs='oops')}/")]
        [TestCase("/items/{value:regex(pattern='^[a-z]+$',timeoutMs=50,caseSensitive='oops')}/")]
        [TestCase("/items/{value:regex(pattern='^[a-z]+$',timeoutMs=50,foo='bar')}/")]
        public void AddDefaultValueParsers_ShouldRejectSemanticallyInvalidBuiltInParserArguments(string pattern)
        {
            _routerBuilder.AddDefaultValueParsers();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("args"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PARSERS_ARGS));
        }

        [TestCase("/items/{value:guid(foo='bar')}/")]
        [TestCase("/items/{value:bool(foo='bar')}/")]
        public void BuiltInParsersWithoutParameters_ShouldRejectArguments(string pattern)
        {
            _routerBuilder
                .AddGuidParser()
                .AddBoolParser();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("rawArgs"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PARSERS_ARGS));
        }

        [TestCase("/items/{value:int(min=10,max=20,foo=1)}/")]
        [TestCase("/items/{value:int(min=10,max=5)}/")]
        [TestCase("/items/{value:int(min='oops')}/")]
        [TestCase("/items/{value:int(max=true)}/")]
        public void AddIntParser_ShouldRejectInvalidArguments(string pattern)
        {
            _routerBuilder.AddIntParser();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("args"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PARSERS_ARGS));
        }

        [TestCase("/items/{value:str(min=5,max=3)}/")]
        [TestCase("/items/{value:str(min='oops')}/")]
        [TestCase("/items/{value:str(max=false)}/")]
        [TestCase("/items/{value:str(pattern='[')}/")]
        [TestCase("/items/{value:str(foo='bar')}/")]
        public void AddStringParser_ShouldRejectInvalidArguments(string pattern)
        {
            _routerBuilder.AddStringParser();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("args"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PARSERS_ARGS));
        }

        [TestCase("/items/{value:regex}/")]
        [TestCase("/items/{value:regex(timeoutMs=50)}/")]
        [TestCase("/items/{value:regex(pattern='[',timeoutMs=50)}/")]
        [TestCase("/items/{value:regex(pattern='^[a-z]+$',timeoutMs=0)}/")]
        [TestCase("/items/{value:regex(pattern='^[a-z]+$',timeoutMs=-1)}/")]
        [TestCase("/items/{value:regex(pattern='^[a-z]+$',timeoutMs='oops')}/")]
        [TestCase("/items/{value:regex(pattern='^[a-z]+$',timeoutMs=50,caseSensitive='oops')}/")]
        [TestCase("/items/{value:regex(pattern='^[a-z]+$',timeoutMs=50,foo='bar')}/")]
        public void AddRegexParser_ShouldRejectInvalidArguments(string pattern)
        {
            _routerBuilder.AddRegexParser();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => _routerBuilder.AddHandler("GET", pattern, new Mock<RequestHandlerDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("args"));
            Assert.That(ex.Message, Does.StartWith(Resources.ERR_INVALID_PARSERS_ARGS));
        }

        [Test]
        public void AddRegexParser_ShouldDefaultTimeoutTo50Milliseconds()
        {
            _routerBuilder.AddRegexParser();

            object parserArguments = _routerBuilder.ValueParsers["regex"].BindArguments(new Dictionary<string, string>
            {
                ["pattern"] = "^[a-z]+$"
            })!;

            Regex regex = (Regex) parserArguments
                .GetType()
                .GetProperty("Pattern")!
                .GetValue(parserArguments)!;

            Assert.That(regex.MatchTimeout, Is.EqualTo(TimeSpan.FromMilliseconds(50)));
        }

        [Test]
        public void AddValueParser_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddValueParser(null!, new Mock<ValueParserDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("parserName"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddValueParser("any", (SyncValueParserDelegate) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("tryParseDelegate"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddValueParser("any", null!, new Mock<SyncValueParserDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("bindArguments"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddValueParser("any", new Mock<BindArgumentsDelegate>(MockBehavior.Strict).Object, (SyncValueParserDelegate) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("tryParseDelegate"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddValueParser("any", null!, new Mock<ValueParserDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("bindArguments"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.AddValueParser("any", new Mock<BindArgumentsDelegate>(MockBehavior.Strict).Object, (ValueParserDelegate) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("tryParseDelegate"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteValueParserExtensions.AddValueParser((RouterBuilder<HttpMessageRouter, RouterConfig>) null!, "any", new Mock<ValueParserDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteValueParserExtensions.AddValueParser((RouterBuilder<HttpMessageRouter, RouterConfig>) null!, "any", new Mock<BindArgumentsDelegate>(MockBehavior.Strict).Object, new Mock<ValueParserDelegate>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));

            ex = Assert.Throws<ArgumentNullException>(() => NanoRouteValueParserExtensions.AddRegexParser((RouterBuilder<HttpMessageRouter, RouterConfig>) null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("routeScopeBuilder"));
        });
    }
}
