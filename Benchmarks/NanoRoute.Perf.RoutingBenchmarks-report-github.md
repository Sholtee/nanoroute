```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32690/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method | Scenario       | RouterFactory        | Mean     | Error    | StdDev   | Median   | Gen0   | Allocated |
|------- |--------------- |--------------------- |---------:|---------:|---------:|---------:|-------:|----------:|
| **Route**  | **SingleLiteral**  | **ASP.NET Core Matcher** | **121.3 ns** |  **1.12 ns** |  **1.05 ns** | **121.6 ns** | **0.0010** |      **24 B** |
| **Route**  | **SingleLiteral**  | **NanoRoute Router**     | **164.0 ns** |  **1.94 ns** |  **1.72 ns** | **164.1 ns** | **0.0172** |     **432 B** |
| **Route**  | **SingleParsed**   | **ASP.NET Core Matcher** | **202.8 ns** |  **1.80 ns** |  **1.69 ns** | **203.2 ns** | **0.0057** |     **144 B** |
| **Route**  | **SingleParsed**   | **NanoRoute Router**     | **205.0 ns** |  **3.26 ns** |  **3.05 ns** | **205.2 ns** | **0.0234** |     **592 B** |
| **Route**  | **ComplexLiteral** | **ASP.NET Core Matcher** | **239.2 ns** |  **1.55 ns** |  **1.38 ns** | **239.0 ns** | **0.0010** |      **24 B** |
| **Route**  | **ComplexLiteral** | **NanoRoute Router**     | **403.8 ns** |  **4.54 ns** |  **4.24 ns** | **401.8 ns** | **0.0172** |     **432 B** |
| **Route**  | **ComplexParsed**  | **ASP.NET Core Matcher** | **391.7 ns** |  **3.28 ns** |  **3.07 ns** | **390.9 ns** | **0.0095** |     **240 B** |
| **Route**  | **ComplexParsed**  | **NanoRoute Router**     | **593.0 ns** | **11.86 ns** | **26.52 ns** | **577.0 ns** | **0.0248** |     **640 B** |
