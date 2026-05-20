/********************************************************************************
* DelegateConversionBenchmarks.cs                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    [MemoryDiagnoser]
    public class DelegateConversionBenchmarks
    {
        #region Private
        private static readonly Task<HttpResponseMessage> s_responseTask = Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        private static CallNextHandlerDelegate? s_sink;

        private readonly CallNextHandlerDelegate _cachedNext;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Capture(CallNextHandlerDelegate next) => s_sink = next;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private Task<HttpResponseMessage> Next() => s_responseTask;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Task<HttpResponseMessage> StaticNext() => s_responseTask;
        #endregion

        public DelegateConversionBenchmarks() => _cachedNext = Next;

        [Benchmark]
        public void InstanceMethodGroup() => Capture(Next);

        [Benchmark(Baseline = true)]
        public void CachedInstanceDelegate() => Capture(_cachedNext);

        [Benchmark]
        public void StaticMethodGroup() => Capture(StaticNext);
    }
}
