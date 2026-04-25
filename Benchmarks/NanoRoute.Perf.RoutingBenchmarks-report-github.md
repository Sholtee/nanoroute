```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method | Scenario       | Mean     | Error   | StdDev  | Gen0   | Allocated |
|------- |--------------- |---------:|--------:|--------:|-------:|----------:|
| **Route**  | **SingleLiteral**  | **283.0 ns** | **5.15 ns** | **4.82 ns** | **0.0491** |     **824 B** |
| **Route**  | **SingleParsed**   | **343.4 ns** | **6.84 ns** | **9.81 ns** | **0.0587** |     **984 B** |
| **Route**  | **ComplexLiteral** | **606.8 ns** | **6.95 ns** | **6.50 ns** | **0.0486** |     **824 B** |
| **Route**  | **ComplexParsed**  | **796.2 ns** | **7.35 ns** | **6.88 ns** | **0.0610** |    **1032 B** |
