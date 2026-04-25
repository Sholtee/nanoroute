/********************************************************************************
* ValueParsersBenchmarks.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Globalization;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    using Internals;

    [MemoryDiagnoser]
    public class ValueParsersBenchmarks
    {
        public enum ScenarioKind
        {
            Int,
            Boolean,
            GuidHyphenated,
            GuidCompact
        }

        private ReadOnlyMemory<char> _value;

        [Params(ScenarioKind.Int, ScenarioKind.Boolean, ScenarioKind.GuidHyphenated, ScenarioKind.GuidCompact)]
        public ScenarioKind Scenario { get; set; }

        [GlobalSetup]
        public void Setup() => _value = Scenario switch
        {
            ScenarioKind.Int => "2147483647".AsMemory(),
            ScenarioKind.Boolean => "true".AsMemory(),
            ScenarioKind.GuidHyphenated => "4a91f2c0-0e3c-4ec8-9f8c-8d2d2f2c7d1a".AsMemory(),
            ScenarioKind.GuidCompact => "4a91f2c00e3c4ec89f8c8d2d2f2c7d1a".AsMemory(),
            _ => throw new ArgumentOutOfRangeException(nameof(Scenario), Scenario, "Unknown value-parser benchmark scenario.")
        };

        [Benchmark]
        public bool ParseWithValueParsers() => Scenario switch
        {
            ScenarioKind.Int => ValueParsers.TryParseInt32(_value, out _),
            ScenarioKind.Boolean => ValueParsers.TryParseBoolean(_value, out _),
            ScenarioKind.GuidHyphenated => ValueParsers.TryParseGuid(_value, out _),
            ScenarioKind.GuidCompact => ValueParsers.TryParseGuid(_value, out _),
            _ => false
        };

        [Benchmark(Baseline = true)]
        public bool ParseWithFramework() => Scenario switch
        {
            ScenarioKind.Int => int.TryParse(_value.Span, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            ScenarioKind.Boolean => bool.TryParse(_value.Span, out _),
            ScenarioKind.GuidHyphenated => Guid.TryParse(_value.Span, out _),
            ScenarioKind.GuidCompact => Guid.TryParse(_value.Span, out _),
            _ => false
        };
    }
}
