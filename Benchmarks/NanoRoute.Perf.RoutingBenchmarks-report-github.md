```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method | Scenario       | RouterFactory        | Mean     | Error    | StdDev   | Median   | Gen0   | Allocated |
|------- |--------------- |--------------------- |---------:|---------:|---------:|---------:|-------:|----------:|
| **Route**  | **SingleLiteral**  | **ASP.NET Core Matcher** | **169.9 ns** | **15.73 ns** | **46.38 ns** | **134.3 ns** | **0.0014** |      **24 B** |
| **Route**  | **SingleLiteral**  | **NanoRoute Router**     | **276.6 ns** |  **2.15 ns** |  **2.01 ns** | **277.1 ns** | **0.0257** |     **432 B** |
| **Route**  | **SingleParsed**   | **ASP.NET Core Matcher** | **190.8 ns** |  **1.03 ns** |  **0.86 ns** | **191.1 ns** | **0.0086** |     **144 B** |
| **Route**  | **SingleParsed**   | **NanoRoute Router**     | **193.7 ns** |  **3.79 ns** |  **5.06 ns** | **193.5 ns** | **0.0353** |     **592 B** |
| **Route**  | **ComplexLiteral** | **ASP.NET Core Matcher** | **377.4 ns** |  **0.97 ns** |  **0.91 ns** | **377.1 ns** | **0.0014** |      **24 B** |
| **Route**  | **ComplexLiteral** | **NanoRoute Router**     | **399.3 ns** |  **5.09 ns** |  **4.25 ns** | **399.0 ns** | **0.0257** |     **432 B** |
| **Route**  | **ComplexParsed**  | **ASP.NET Core Matcher** | **422.6 ns** |  **2.59 ns** |  **2.16 ns** | **422.2 ns** | **0.0143** |     **240 B** |
| **Route**  | **ComplexParsed**  | **NanoRoute Router**     | **978.0 ns** | **19.42 ns** | **29.66 ns** | **977.1 ns** | **0.0381** |     **640 B** |
