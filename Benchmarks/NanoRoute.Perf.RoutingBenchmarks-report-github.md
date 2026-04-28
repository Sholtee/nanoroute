```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method | Scenario       | RouterFactory        | Mean     | Error    | StdDev    | Median   | Gen0   | Allocated |
|------- |--------------- |--------------------- |---------:|---------:|----------:|---------:|-------:|----------:|
| **Route**  | **SingleLiteral**  | **ASP.NET Core Matcher** | **239.5 ns** |  **0.63 ns** |   **0.53 ns** | **239.5 ns** | **0.0010** |      **24 B** |
| **Route**  | **SingleLiteral**  | **NanoRoute Router**     | **265.2 ns** |  **5.13 ns** |   **4.80 ns** | **263.2 ns** | **0.0257** |     **648 B** |
| **Route**  | **SingleParsed**   | **ASP.NET Core Matcher** | **204.8 ns** |  **2.98 ns** |   **2.49 ns** | **204.1 ns** | **0.0057** |     **144 B** |
| **Route**  | **SingleParsed**   | **NanoRoute Router**     | **380.4 ns** | **30.39 ns** |  **89.61 ns** | **314.3 ns** | **0.0319** |     **808 B** |
| **Route**  | **ComplexLiteral** | **ASP.NET Core Matcher** | **311.2 ns** | **26.95 ns** |  **79.46 ns** | **243.1 ns** | **0.0010** |      **24 B** |
| **Route**  | **ComplexLiteral** | **NanoRoute Router**     | **790.6 ns** | **78.66 ns** | **231.93 ns** | **592.1 ns** | **0.0257** |     **648 B** |
| **Route**  | **ComplexParsed**  | **ASP.NET Core Matcher** | **542.5 ns** | **50.84 ns** | **149.91 ns** | **415.4 ns** | **0.0095** |     **240 B** |
| **Route**  | **ComplexParsed**  | **NanoRoute Router**     | **727.0 ns** |  **5.71 ns** |   **5.06 ns** | **725.1 ns** | **0.0334** |     **856 B** |
