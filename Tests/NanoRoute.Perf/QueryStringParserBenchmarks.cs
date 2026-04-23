/********************************************************************************
* QueryStringParserBenchmarks.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        private static readonly IReadOnlyDictionary<ReadOnlyMemory<char>, ParameterParser>
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
        public async ValueTask Parse_RequiredParameterMissing()
        {
            try
            {
                await QueryStringParser.Parse(CreateContext(s_missingRequiredUri), s_missingRequiredExpected).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
            }
        }

        private static Dictionary<ReadOnlyMemory<char>, ParameterParser> CreateExpectedParameters(params (string Name, bool Optional)[] parameters)
        {
            Dictionary<ReadOnlyMemory<char>, ParameterParser> result = new(ReadOnlyMemoryCharComparer.Instance);

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterDefinition definition = new()
                {
                    ValueParser = new()
                    {
                        Name = "str",
                        RawArguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    },
                    ParameterName = parameters[i].Name,
                    IsOptional = parameters[i].Optional,
                    Index = i
                };

                result.Add
                (
                    definition.ParameterName!.AsMemory(),
                    new ParameterParser
                    (
                        definition,
                        static context => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.DecodedSegment.ToString())),
                        Arguments: null
                    )
                );
            }

            return result;
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The request has no body to dispose")]
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

