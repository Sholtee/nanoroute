/********************************************************************************
* EnumParsingBenchmark.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    using Internals;

    [MemoryDiagnoser]
    public class EnumParsingBenchmarks
    {
        [Params("GET", "Get", "Invalid")]
        public string Value { get; set; } = null!;

        [Benchmark(Baseline = true)]
        public bool BclEnumTryParse() => Enum.TryParse(Value, ignoreCase: true, out HttpVerb _);

        [Benchmark]
        public bool TryParseFast() => HttpVerb.TryParseFast(Value, out HttpVerb _);
    }
}
