```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4


```
| Method | ScenarioKind   | RouteMatcherFactory    | Mean      | Error    | StdDev   | Gen0   | Allocated |
|------- |--------------- |----------------------- |----------:|---------:|---------:|-------:|----------:|
| **Match**  | **SingleLiteral**  | **ASP.NET Core Matcher**   | **107.43 ns** | **2.065 ns** | **2.611 ns** |      **-** |         **-** |
| **Match**  | **SingleLiteral**  | **NanoRoute Match Cursor** |  **88.82 ns** | **1.415 ns** | **1.181 ns** | **0.0054** |     **136 B** |
| **Match**  | **SingleParsed**   | **ASP.NET Core Matcher**   | **191.39 ns** | **2.991 ns** | **4.193 ns** | **0.0043** |     **112 B** |
| **Match**  | **SingleParsed**   | **NanoRoute Match Cursor** | **102.72 ns** | **0.596 ns** | **0.498 ns** | **0.0063** |     **160 B** |
| **Match**  | **ComplexLiteral** | **ASP.NET Core Matcher**   | **224.91 ns** | **1.312 ns** | **1.163 ns** |      **-** |         **-** |
| **Match**  | **ComplexLiteral** | **NanoRoute Match Cursor** | **248.27 ns** | **3.208 ns** | **3.001 ns** | **0.0052** |     **136 B** |
| **Match**  | **ComplexParsed**  | **ASP.NET Core Matcher**   | **358.87 ns** | **1.053 ns** | **0.934 ns** | **0.0076** |     **192 B** |
| **Match**  | **ComplexParsed**  | **NanoRoute Match Cursor** | **332.46 ns** | **6.417 ns** | **7.880 ns** | **0.0081** |     **208 B** |
