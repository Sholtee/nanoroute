/********************************************************************************
* UriSegmentBenchmarks.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    using Internals;

    [MemoryDiagnoser]
    public class UriSegmentBenchmarks
    {
        [Params("/api/users/42/orders/7")]
        public string Path { get; set; } = string.Empty;

        [Benchmark]
        public int WalkSegments()
        {
            int total = 0;

            for (UriSegment? current = new UriSegment(Path); current?.Value is not null; current = current.Next)
                total += current.Value.Length;

            return total;
        }

        [Benchmark(Baseline = true)]
        public int SplitSegments()
        {
            int total = 0;

            foreach (string segment in Path.Split([UriSegment.SEPARATOR], StringSplitOptions.RemoveEmptyEntries))
                total += segment.Length;

            return total;
        }
    }
}
