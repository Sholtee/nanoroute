/********************************************************************************
* QueryStringParserBenchmarks.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Frozen;
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
        public enum ScenarioKind
        {
            AllParametersProvided,
            OptionalParameterMissing,
            UndeclaredParametersPresent,
            RequiredParameterMissing
        }

        private static readonly IServiceProvider s_services = new NoopServiceProvider();

        private FrozenDictionary<ReadOnlyMemory<char>, ParameterParser> _expected = null!;

        private Uri _uri = null!;

        private bool _expectBadRequest;

        [Params(ScenarioKind.AllParametersProvided, ScenarioKind.OptionalParameterMissing, ScenarioKind.UndeclaredParametersPresent, ScenarioKind.RequiredParameterMissing)]
        public ScenarioKind Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            (_uri, _expected, _expectBadRequest) = Scenario switch
            {
                ScenarioKind.AllParametersProvided => new ValueTuple<Uri, FrozenDictionary<ReadOnlyMemory<char>, ParameterParser>, bool>
                (
                    new Uri("https://www.example.com/items?filter=active&page=2&optional=extra", UriKind.Absolute),
                    CreateExpectedParameters
                    (
                        ("filter", false),
                        ("page", false),
                        ("optional", true)
                    ),
                    false
                ),

                ScenarioKind.OptionalParameterMissing => new ValueTuple<Uri, FrozenDictionary<ReadOnlyMemory<char>, ParameterParser>, bool>
                (
                    new Uri("https://www.example.com/items?filter=active", UriKind.Absolute),
                    CreateExpectedParameters
                    (
                        ("filter", false),
                        ("optional", true)
                    ),
                    false
                ),

                ScenarioKind.UndeclaredParametersPresent => new ValueTuple<Uri, FrozenDictionary<ReadOnlyMemory<char>, ParameterParser>, bool>
                (
                    new Uri("https://www.example.com/items?filter=active&extra=1&debug=true&trace=abc", UriKind.Absolute),
                    CreateExpectedParameters
                    (
                        ("filter", false),
                        ("optional", true)
                    ),
                    false
                ),

                ScenarioKind.RequiredParameterMissing => new ValueTuple<Uri, FrozenDictionary<ReadOnlyMemory<char>, ParameterParser>, bool>
                (
                    new Uri("https://www.example.com/items?filter=active", UriKind.Absolute),
                    CreateExpectedParameters
                    (
                        ("filter", false),
                        ("page", false)
                    ),
                    true
                ),

                _ => throw new ArgumentOutOfRangeException(nameof(Scenario), Scenario, "Unknown query-string benchmark scenario.")
            };
        }

        [Benchmark]
        public async ValueTask Parse()
        {
            try
            {
                using QueryStringParser parser = new(CreateContext(_uri), _expected, UnexpectedParameterBehavior.Ignore);

                await parser.Parse().ConfigureAwait(false);
            }
            catch (HttpRequestException) when (_expectBadRequest)
            {
            }
        }

        private static FrozenDictionary<ReadOnlyMemory<char>, ParameterParser> CreateExpectedParameters(params (string Name, bool Optional)[] parameters)
        {
            Dictionary<ReadOnlyMemory<char>, ParameterParser> result = new(ReadOnlyMemoryCharComparer.Instance);

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterDefinition definition = new()
                {
                    ValueParser = new()
                    {
                        Name = "str",
                        IsList = false,
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
                        static context => new ValueTask<ValueParseResult>(new ValueParseResult(true, context.Segment.ToString())),
                        Arguments: null
                    )
                );
            }

            return result.ToFrozenDictionary();
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The request has no body to dispose")]
        private static RequestContext CreateContext(Uri uri) => new()
        {
            Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            Services = s_services,
            Request = new HttpRequestMessage(HttpMethod.Get, uri),
            RemainingPath = default,
            Cancellation = CancellationToken.None
        };

        private sealed class NoopServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }
    }
}

