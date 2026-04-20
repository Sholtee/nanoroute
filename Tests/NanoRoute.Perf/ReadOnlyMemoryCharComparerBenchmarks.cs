/********************************************************************************
* ReadOnlyMemoryCharComparerBenchmarks.cs                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    using Internals;

    [MemoryDiagnoser]
    public class ReadOnlyMemoryCharComparerBenchmarks
    {
        public enum ScenarioKind
        {
            AsciiEqual,
            AsciiDifferent,
            NonAsciiEqual,
            NonAsciiDifferent,
            MixedEqual
        }

        private readonly ReadOnlyMemoryCharComparer _comparer = ReadOnlyMemoryCharComparer.Instance;

        private Dictionary<ReadOnlyMemory<char>, int> _dictionary = null!;

        private ReadOnlyMemory<char>
            _left,
            _right;

        [Params(ScenarioKind.AsciiEqual, ScenarioKind.AsciiDifferent, ScenarioKind.NonAsciiEqual, ScenarioKind.NonAsciiDifferent, ScenarioKind.MixedEqual)]
        public ScenarioKind Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            (_left, _right) = Scenario switch
            {
                ScenarioKind.AsciiEqual => ("warehouse".AsMemory(), "WAREHOUSE".AsMemory()),
                ScenarioKind.AsciiDifferent => ("warehouse".AsMemory(), "currency".AsMemory()),
                ScenarioKind.NonAsciiEqual => ("café".AsMemory(), "CAFÉ".AsMemory()),
                ScenarioKind.NonAsciiDifferent => ("café".AsMemory(), "cafe\u0301".AsMemory()),
                ScenarioKind.MixedEqual => ("raktár-42-café".AsMemory(), "RAKTÁR-42-CAFÉ".AsMemory()),
                _ => throw new ArgumentOutOfRangeException(nameof(Scenario), Scenario, "Unknown comparer benchmark scenario.")
            };

            _dictionary = new Dictionary<ReadOnlyMemory<char>, int>(_comparer)
            {
                [_left] = 42
            };
        }

        [Benchmark]
        public bool Equals() => _comparer.Equals(_left, _right);

        [Benchmark]
        public int ComputeHashCode() => _comparer.GetHashCode(_left);

        [Benchmark]
        public bool DictionaryLookup() => _dictionary.TryGetValue(_right, out _);
    }
}
