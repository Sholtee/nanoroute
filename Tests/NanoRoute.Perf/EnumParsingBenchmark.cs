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

    public class EnumParsingBenchmark
    {
        [Params("Get", "Invalid")]
        public string Value { get; set; } = null!;

        [Benchmark]
        public bool TryParse() => Enum.TryParse(Value, ignoreCase: true, out HttpVerb _);
    }
}
