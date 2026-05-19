/********************************************************************************
* DelimitedSegmentBenchmarks.cs                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    using Internals;

    [MemoryDiagnoser]
    public class DelimitedSegmentBenchmarks
    {
        [Params("/api/users/42/orders/7")]
        public string Path { get; set; } = null!;

        [Benchmark]
        public int WalkSegments()
        {
            int total = 0;

            for (DelimitedSegment current = new(Path.AsMemory(), '/'); current.MoveNext();)
                total += current.Current.Length;

            return total;
        }

        [Benchmark(Baseline = true)]
        public int SplitSegments()
        {
            int total = 0;

            foreach (string segment in Path.Split(['/'], StringSplitOptions.RemoveEmptyEntries))
                total += segment.Length;

            return total;
        }
    }

    [MemoryDiagnoser]
    public class UrlSegmentBenchmarks
    {
        public enum ScenarioKind
        {
            Short,
            Long,
            Escaped
        }

        private Uri _uri = null!;

        private string _absolutePath = null!;

        [Params(ScenarioKind.Short, ScenarioKind.Long, ScenarioKind.Escaped)]
        public ScenarioKind Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _uri = new Uri
            (
                Scenario switch
                {
                    ScenarioKind.Short => "https://example.com/api/users/42/orders/7",
                    ScenarioKind.Long => "https://example.com/api/v1/tenants/alpha/regions/eu-central-1/warehouses/12/orders/2026/05/19/items/42/details",
                    ScenarioKind.Escaped => "https://example.com/files/path%2Fto%2Fsomewhere/users/jane%20doe/orders/7",
                    _ => throw new ArgumentOutOfRangeException(nameof(Scenario), Scenario, "Unknown URL segment benchmark scenario.")
                }
            );

            _absolutePath = _uri.AbsolutePath;
        }

        [Benchmark(Baseline = true)]
        public int UriSegments()
        {
            int total = 0;

            foreach (string segment in _uri.Segments)
                total += segment.Length;

            return total;
        }

        [Benchmark]
        public int DelimitedSegment()
        {
            int total = 0;

            for (DelimitedSegment current = new(_absolutePath.AsMemory(), '/'); current.MoveNext();)
                total += current.Current.Length;

            return total;
        }
    }
}
