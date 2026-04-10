/********************************************************************************
* Program.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace NanoRoute.Perf
{
    static class Program
    {
        static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run
        (
            args,
#if DEBUG
            new DebugInProcessConfig()
#else
            DefaultConfig.Instance.WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(100))
#endif
        );
    }
}
