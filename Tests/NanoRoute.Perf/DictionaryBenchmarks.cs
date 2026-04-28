/********************************************************************************
* DictionaryBenchmarks.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Frozen;
using System.Collections.Generic;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    using Internals;

    public class DictionaryBenchmarks
    {
        private readonly IReadOnlyDictionary<ReadOnlyMemory<char>, object> _stringKeyedDict = new Dictionary<ReadOnlyMemory<char>, object>(ReadOnlyMemoryCharComparer.Instance)
        {
            ["segment_1".AsMemory()] = null!,
            ["segment_2".AsMemory()] = null!
        }.ToFrozenDictionary(ReadOnlyMemoryCharComparer.Instance);

        private readonly IReadOnlyDictionary<HttpVerb, object> _verbKeyedDict = new Dictionary<HttpVerb, object>
        {
            [HttpVerb.Get] = null!,
            [HttpVerb.Patch] = null!
        }.ToFrozenDictionary();

        [Benchmark]
        public object StringKeyedDictionary() => _stringKeyedDict["segment_1".AsMemory()];

        [Benchmark]
        public object VerbKeyedDictionary() => _verbKeyedDict[HttpVerb.Get];
    }
}
