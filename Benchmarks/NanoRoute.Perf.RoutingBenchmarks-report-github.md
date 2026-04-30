```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method | Scenario       | RouterFactory        | Mean     | Error   | StdDev  | Gen0   | Allocated |
|------- |--------------- |--------------------- |---------:|--------:|--------:|-------:|----------:|
| **Route**  | **SingleLiteral**  | **ASP.NET Core Matcher** | **102.7 ns** | **1.85 ns** | **1.64 ns** | **0.0014** |      **24 B** |
| **Route**  | **SingleLiteral**  | **NanoRoute Router**     | **104.6 ns** | **1.14 ns** | **1.01 ns** | **0.0257** |     **432 B** |
| **Route**  | **SingleParsed**   | **ASP.NET Core Matcher** | **169.6 ns** | **1.04 ns** | **0.98 ns** | **0.0086** |     **144 B** |
| **Route**  | **SingleParsed**   | **NanoRoute Router**     | **123.8 ns** | **1.51 ns** | **1.42 ns** | **0.0353** |     **592 B** |
| **Route**  | **ComplexLiteral** | **ASP.NET Core Matcher** | **194.7 ns** | **1.90 ns** | **1.78 ns** | **0.0014** |      **24 B** |
| **Route**  | **ComplexLiteral** | **NanoRoute Router**     | **305.8 ns** | **1.84 ns** | **1.53 ns** | **0.0257** |     **432 B** |
| **Route**  | **ComplexParsed**  | **ASP.NET Core Matcher** | **307.1 ns** | **6.06 ns** | **8.09 ns** | **0.0143** |     **240 B** |
| **Route**  | **ComplexParsed**  | **NanoRoute Router**     | **402.1 ns** | **4.09 ns** | **3.62 ns** | **0.0381** |     **640 B** |
