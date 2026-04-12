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

        private static readonly SegmentParser s_parser = new
        (
            SegmentParserDefinition.Create("{value:str}"),
            static context => new ValueTask<SegmentParseResult>(new SegmentParseResult(true, context.DecodedSegment.ToString())),
            Arguments: null
        );

        private static readonly Dictionary<string, QueryParameterDefinition> s_allExpected = CreateExpectedParameters
        (
            new QueryParameterDefinition("filter", Optional: false, s_parser),
            new QueryParameterDefinition("page", Optional: false, s_parser),
            new QueryParameterDefinition("optional", Optional: true, s_parser)
        );

        private static readonly Dictionary<string, QueryParameterDefinition> s_optionalExpected = CreateExpectedParameters
        (
            new QueryParameterDefinition("filter", Optional: false, s_parser),
            new QueryParameterDefinition("optional", Optional: true, s_parser)
        );

        private static readonly Dictionary<string, QueryParameterDefinition> s_missingRequiredExpected = CreateExpectedParameters
        (
            new QueryParameterDefinition("filter", Optional: false, s_parser),
            new QueryParameterDefinition("page", Optional: false, s_parser)
        );

        private static readonly Uri
            s_allParametersUri = new("https://www.example.com/items?filter=active&page=2&optional=extra", UriKind.Absolute),
            s_optionalMissingUri = new("https://www.example.com/items?filter=active", UriKind.Absolute),
            s_undeclaredPresentUri = new("https://www.example.com/items?filter=active&extra=1&debug=true&trace=abc", UriKind.Absolute),
            s_missingRequiredUri = new("https://www.example.com/items?filter=active", UriKind.Absolute);

        [Benchmark(Baseline = true)]
        public ValueTask<Dictionary<string, object?>> Parse_AllParametersProvided() =>
            QueryStringParser.Parse(s_allParametersUri, s_allExpected, s_services, CancellationToken.None);

        [Benchmark]
        public ValueTask<Dictionary<string, object?>> Parse_OptionalParameterMissing() =>
            QueryStringParser.Parse(s_optionalMissingUri, s_optionalExpected, s_services, CancellationToken.None);

        [Benchmark]
        public ValueTask<Dictionary<string, object?>> Parse_UndeclaredParametersPresent() =>
            QueryStringParser.Parse(s_undeclaredPresentUri, s_optionalExpected, s_services, CancellationToken.None);

        [Benchmark]
        public async Task<bool> Parse_RequiredParameterMissing()
        {
            try
            {
                await QueryStringParser.Parse(s_missingRequiredUri, s_missingRequiredExpected, s_services, CancellationToken.None).ConfigureAwait(false);
                return false;
            }
            catch (HttpRequestException)
            {
                return true;
            }
        }

        private static Dictionary<string, QueryParameterDefinition> CreateExpectedParameters(params QueryParameterDefinition[] parameters)
        {
            Dictionary<string, QueryParameterDefinition> result = new(StringComparer.OrdinalIgnoreCase);

            foreach (QueryParameterDefinition parameter in parameters)
                result.Add(parameter.Name, parameter);

            return result;
        }

        private sealed class NoopServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }
    }
}
