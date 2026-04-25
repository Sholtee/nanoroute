/********************************************************************************
* ReadOnlyMemoryCharComparerBenchmarks.cs                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace NanoRoute.Perf
{
    using Internals;

    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [MemoryDiagnoser]
    public class ReadOnlyMemoryCharComparerBenchmarks
    {
        public enum ScenarioKind
        {
            AsciiEqual,
            AsciiDifferent,
            NonAsciiEqual,
            NonAsciiDifferent
        }

        private string
            _leftString = null!,
            _rightString = null!;

        [Params(ScenarioKind.AsciiEqual, ScenarioKind.AsciiDifferent, ScenarioKind.NonAsciiEqual, ScenarioKind.NonAsciiDifferent)]
        public ScenarioKind Scenario { get; set; }

        [GlobalSetup]
        public void Setup() => (_leftString, _rightString) = Scenario switch
        {
            ScenarioKind.AsciiEqual => ("warehouse", "WAREHOUSE"),
            ScenarioKind.AsciiDifferent => ("warehouse", "currency"),
            ScenarioKind.NonAsciiEqual => ("café", "CAFÉ"),
            ScenarioKind.NonAsciiDifferent => ("café", "cafe\u0301"),
            _ => throw new ArgumentOutOfRangeException(nameof(Scenario), Scenario, "Unknown comparer benchmark scenario.")
        };

        [BenchmarkCategory(nameof(Equals))]
        [Benchmark(Baseline = true)]
        public bool FrameworkEquals() => StringComparer.OrdinalIgnoreCase.Equals(_leftString, _rightString);

        [BenchmarkCategory(nameof(Equals))]
        [Benchmark]
        public bool Equals() => ReadOnlyMemoryCharComparer.Instance.Equals(_leftString.AsMemory(), _rightString.AsMemory());

        [BenchmarkCategory(nameof(ComputeHashCode))]
        [Benchmark(Baseline = true)]
        public int FrameworkComputeHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_leftString);

        [BenchmarkCategory(nameof(ComputeHashCode))]
        [Benchmark]
        public int ComputeHashCode() => ReadOnlyMemoryCharComparer.Instance.GetHashCode(_leftString.AsMemory());
    }
}
