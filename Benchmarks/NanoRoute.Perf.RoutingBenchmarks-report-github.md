```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4


```
| Method | Scenario       | RouterFactory        | Mean     | Error   | StdDev  | Gen0   | Allocated |
|------- |--------------- |--------------------- |---------:|--------:|--------:|-------:|----------:|
| **Route**  | **SingleLiteral**  | **ASP.NET Core Matcher** | **117.7 ns** | **0.38 ns** | **0.33 ns** | **0.0010** |      **24 B** |
| **Route**  | **SingleLiteral**  | **NanoRoute Router**     | **192.6 ns** | **3.49 ns** | **3.26 ns** | **0.0260** |     **656 B** |
| **Route**  | **SingleParsed**   | **ASP.NET Core Matcher** | **200.2 ns** | **0.26 ns** | **0.23 ns** | **0.0057** |     **144 B** |
| **Route**  | **SingleParsed**   | **NanoRoute Router**     | **222.1 ns** | **2.63 ns** | **2.46 ns** | **0.0269** |     **680 B** |
| **Route**  | **ComplexLiteral** | **ASP.NET Core Matcher** | **237.1 ns** | **0.23 ns** | **0.21 ns** | **0.0010** |      **24 B** |
| **Route**  | **ComplexLiteral** | **NanoRoute Router**     | **467.0 ns** | **1.86 ns** | **1.74 ns** | **0.0257** |     **656 B** |
| **Route**  | **ComplexParsed**  | **ASP.NET Core Matcher** | **392.7 ns** | **0.50 ns** | **0.47 ns** | **0.0095** |     **240 B** |
| **Route**  | **ComplexParsed**  | **NanoRoute Router**     | **617.7 ns** | **3.22 ns** | **2.69 ns** | **0.0286** |     **728 B** |
