```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method | Scenario       | Mean       | Error    | StdDev    | Median     | Gen0   | Allocated |
|------- |--------------- |-----------:|---------:|----------:|-----------:|-------:|----------:|
| **Route**  | **SingleLiteral**  |   **308.5 ns** |  **3.53 ns** |   **3.30 ns** |   **307.7 ns** | **0.0491** |     **824 B** |
| **Route**  | **SingleParsed**   |   **464.0 ns** | **39.03 ns** | **115.09 ns** |   **375.0 ns** | **0.0587** |     **984 B** |
| **Route**  | **ComplexLiteral** |   **744.5 ns** | **73.73 ns** | **217.40 ns** |   **594.9 ns** | **0.0477** |     **824 B** |
| **Route**  | **ComplexParsed**  | **1,081.9 ns** | **91.14 ns** | **268.74 ns** | **1,227.1 ns** | **0.0610** |    **1032 B** |
