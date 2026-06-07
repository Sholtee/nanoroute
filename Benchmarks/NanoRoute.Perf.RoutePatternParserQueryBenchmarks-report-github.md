```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method            | Scenario          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------ |------------------ |-----------:|---------:|---------:|-------:|----------:|
| **ParseQueryPattern** | **Empty**             |         **NA** |       **NA** |       **NA** |     **NA** |        **NA** |
| **ParseQueryPattern** | **SingleParameter**   |   **546.5 ns** |  **3.13 ns** |  **2.78 ns** | **0.0801** |   **1.31 KB** |
| **ParseQueryPattern** | **OptionalParameter** |   **689.7 ns** | **12.16 ns** | **11.38 ns** | **0.0963** |   **1.59 KB** |
| **ParseQueryPattern** | **ListParameter**     |   **573.2 ns** |  **8.28 ns** |  **7.75 ns** | **0.0811** |   **1.34 KB** |
| **ParseQueryPattern** | **ParserArguments**   | **2,246.0 ns** | **34.49 ns** | **30.57 ns** | **0.2975** |   **4.89 KB** |
| **ParseQueryPattern** | **ManyParameters**    | **3,994.4 ns** | **69.04 ns** | **61.20 ns** | **0.5341** |   **8.73 KB** |

Benchmarks with issues:
  RoutePatternParserQueryBenchmarks.ParseQueryPattern: DefaultJob [Scenario=Empty]
