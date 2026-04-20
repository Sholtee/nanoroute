/********************************************************************************
* UrlUtilsBenchmarks.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    using Internals;

    [MemoryDiagnoser]
    public class UrlUtilsBenchmarks
    {
        public enum ScenarioKind
        {
            Plain,
            Plus,
            AsciiEscaped,
            Utf8Escaped,
            Mixed
        }

        private char[] _buffer = [];

        private string _source = string.Empty;

        [Params(ScenarioKind.Plain, ScenarioKind.Plus, ScenarioKind.AsciiEscaped, ScenarioKind.Utf8Escaped, ScenarioKind.Mixed)]
        public ScenarioKind Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _source = Scenario switch
            {
                ScenarioKind.Plain => "filter=active&page=42&currency=HUF&warehouse=12",
                ScenarioKind.Plus => "filter=active+items&page=42&currency=Hungarian+Forint&warehouse=12",
                ScenarioKind.AsciiEscaped => "filter=active%20items&page=42&currency=HUF&warehouse=12%2Fmain",
                ScenarioKind.Utf8Escaped => "name=caf%C3%A9&emoji=%F0%9F%98%80&city=Budapest",
                ScenarioKind.Mixed => "filter=caf%C3%A9+items&page=42&path=%2Fwarehouses%2F12&emoji=%F0%9F%98%80",
                _ => throw new ArgumentOutOfRangeException(nameof(Scenario), Scenario, "Unknown URL decoding benchmark scenario.")
            };

            _buffer = new char[_source.Length];
        }

        [Benchmark]
        public int Decode()
        {
            if (!UrlUtils.TryDecodeUrl(_source, _buffer, out int charsWritten))
                throw new InvalidOperationException("Could not decode benchmark input.");

            return charsWritten;
        }
    }
}
