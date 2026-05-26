```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method            | Scenario          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------ |------------------ |-----------:|---------:|---------:|-------:|----------:|
| **ParseQueryPattern** | **Empty**             |         **NA** |       **NA** |       **NA** |     **NA** |        **NA** |
| **ParseQueryPattern** | **SingleParameter**   |   **578.9 ns** |  **3.36 ns** |  **2.98 ns** | **0.0801** |   **1.31 KB** |
| **ParseQueryPattern** | **OptionalParameter** |   **766.3 ns** | **11.59 ns** | **10.27 ns** | **0.0963** |   **1.59 KB** |
| **ParseQueryPattern** | **ListParameter**     |   **601.0 ns** |  **2.89 ns** |  **2.56 ns** | **0.0811** |   **1.34 KB** |
| **ParseQueryPattern** | **ParserArguments**   | **2,431.4 ns** | **20.05 ns** | **18.75 ns** | **0.2975** |   **4.89 KB** |
| **ParseQueryPattern** | **ManyParameters**    | **4,021.8 ns** | **80.10 ns** | **92.25 ns** | **0.5341** |   **8.73 KB** |

Benchmarks with issues:
  RoutePatternParserQueryBenchmarks.ParseQueryPattern: DefaultJob [Scenario=Empty]
