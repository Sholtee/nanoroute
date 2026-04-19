/********************************************************************************
* QueryStringParserBenchmarks.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    using Internals;

    [MemoryDiagnoser]
    public class QueryStringParserBenchmarks
    {
        private static readonly IServiceProvider s_services = new NoopServiceProvider();

        private static readonly ValueParserDefinition s_valueParserDefinition = ParseValue("str");

        private static readonly ValueParser s_parser = new
        (
            s_valueParserDefinition,
            static context => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.DecodedSegment.ToString())),
            Arguments: null
        );

        private static ValueParserDefinition ParseValue(string definition)
        {
            int offset = 0;
            ValueParserDefinition result = ValueParserDefinition.Parse(definition, ref offset);

            if (offset != definition.Length)
                throw new InvalidOperationException();

            return result;
        }

        private static readonly IReadOnlyDictionary<string, QueryParameterDefinition>
            s_allExpected = CreateExpectedParameters
            (
                ("filter", false),
                ("page", false),
                ("optional", true)
            ),
            s_optionalExpected = CreateExpectedParameters
            (
                ("filter", false),
                ("optional", true)
            ),
            s_missingRequiredExpected = CreateExpectedParameters
            (
                ("filter", false),
                ("page", false)
            );

        private static readonly Uri
            s_allParametersUri = new("https://www.example.com/items?filter=active&page=2&optional=extra", UriKind.Absolute),
            s_optionalMissingUri = new("https://www.example.com/items?filter=active", UriKind.Absolute),
            s_undeclaredPresentUri = new("https://www.example.com/items?filter=active&extra=1&debug=true&trace=abc", UriKind.Absolute),
            s_missingRequiredUri = new("https://www.example.com/items?filter=active", UriKind.Absolute);

        [Benchmark(Baseline = true)]
        public ValueTask Parse_AllParametersProvided() =>
            QueryStringParser.Parse(CreateContext(s_allParametersUri), s_allExpected);

        [Benchmark]
        public ValueTask Parse_OptionalParameterMissing() =>
            QueryStringParser.Parse(CreateContext(s_optionalMissingUri), s_optionalExpected);

        [Benchmark]
        public ValueTask Parse_UndeclaredParametersPresent() =>
            QueryStringParser.Parse(CreateContext(s_undeclaredPresentUri), s_optionalExpected);

        [Benchmark]
        public async ValueTask<bool> Parse_RequiredParameterMissing()
        {
            try
            {
                await QueryStringParser.Parse(CreateContext(s_missingRequiredUri), s_missingRequiredExpected).ConfigureAwait(false);
                return false;
            }
            catch (HttpRequestException)
            {
                return true;
            }
        }

        private static Dictionary<string, QueryParameterDefinition> CreateExpectedParameters(params (string Name, bool Optional)[] parameters)
        {
            Dictionary<string, QueryParameterDefinition> result = new(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < parameters.Length; i++)
            {
                QueryParameterDefinition parameter = new(parameters[i].Name, i, parameters[i].Optional, s_parser);
                result.Add(parameter.Name, parameter);
            }

            return result;
        }

        private static RequestContext CreateContext(Uri uri) => new
        (
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            s_services,
            new HttpRequestMessage(HttpMethod.Get, uri),
            CancellationToken.None
        );

        private sealed class NoopServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }
    }
}

