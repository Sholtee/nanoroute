```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4


```
| Method            | Scenario          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------ |------------------ |-----------:|---------:|---------:|-------:|----------:|
| **ParseQueryPattern** | **Empty**             |         **NA** |       **NA** |       **NA** |     **NA** |        **NA** |
| **ParseQueryPattern** | **SingleParameter**   |   **663.1 ns** |  **4.04 ns** |  **3.78 ns** | **0.0534** |   **1.31 KB** |
| **ParseQueryPattern** | **OptionalParameter** |   **805.0 ns** |  **7.81 ns** |  **7.31 ns** | **0.0639** |   **1.59 KB** |
| **ParseQueryPattern** | **ListParameter**     |   **657.1 ns** | **12.14 ns** | **11.36 ns** | **0.0544** |   **1.34 KB** |
| **ParseQueryPattern** | **ParserArguments**   | **2,681.3 ns** | **21.88 ns** | **19.39 ns** | **0.1984** |   **4.89 KB** |
| **ParseQueryPattern** | **ManyParameters**    | **4,410.6 ns** | **17.02 ns** | **15.92 ns** | **0.3510** |   **8.73 KB** |

Benchmarks with issues:
  RoutePatternParserQueryBenchmarks.ParseQueryPattern: DefaultJob [Scenario=Empty]
