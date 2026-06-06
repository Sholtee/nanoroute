```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method | ScenarioKind   | RouteMatcherFactory    | Mean      | Error    | StdDev   | Gen0   | Allocated |
|------- |--------------- |----------------------- |----------:|---------:|---------:|-------:|----------:|
| **Match**  | **SingleLiteral**  | **ASP.NET Core Matcher**   | **112.78 ns** | **0.144 ns** | **0.120 ns** |      **-** |         **-** |
| **Match**  | **SingleLiteral**  | **NanoRoute Match Cursor** |  **84.48 ns** | **1.505 ns** | **1.408 ns** | **0.0081** |     **136 B** |
| **Match**  | **SingleParsed**   | **ASP.NET Core Matcher**   | **175.10 ns** | **0.774 ns** | **0.686 ns** | **0.0067** |     **112 B** |
| **Match**  | **SingleParsed**   | **NanoRoute Match Cursor** |  **99.07 ns** | **1.252 ns** | **1.171 ns** | **0.0095** |     **160 B** |
| **Match**  | **ComplexLiteral** | **ASP.NET Core Matcher**   | **220.08 ns** | **0.262 ns** | **0.232 ns** |      **-** |         **-** |
| **Match**  | **ComplexLiteral** | **NanoRoute Match Cursor** | **247.48 ns** | **1.213 ns** | **1.135 ns** | **0.0081** |     **136 B** |
| **Match**  | **ComplexParsed**  | **ASP.NET Core Matcher**   | **345.37 ns** | **2.003 ns** | **1.874 ns** | **0.0114** |     **192 B** |
| **Match**  | **ComplexParsed**  | **NanoRoute Match Cursor** | **325.39 ns** | **1.366 ns** | **1.278 ns** | **0.0124** |     **208 B** |
