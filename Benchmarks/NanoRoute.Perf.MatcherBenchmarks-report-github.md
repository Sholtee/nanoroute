```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method | ScenarioKind   | RouteMatcherFactory    | Mean      | Error    | StdDev   | Gen0   | Allocated |
|------- |--------------- |----------------------- |----------:|---------:|---------:|-------:|----------:|
| **Match**  | **SingleLiteral**  | **ASP.NET Core Matcher**   | **119.01 ns** | **0.254 ns** | **0.226 ns** |      **-** |         **-** |
| **Match**  | **SingleLiteral**  | **NanoRoute Match Cursor** |  **82.98 ns** | **0.170 ns** | **0.159 ns** | **0.0081** |     **136 B** |
| **Match**  | **SingleParsed**   | **ASP.NET Core Matcher**   | **189.15 ns** | **0.329 ns** | **0.308 ns** | **0.0067** |     **112 B** |
| **Match**  | **SingleParsed**   | **NanoRoute Match Cursor** |  **96.77 ns** | **0.156 ns** | **0.130 ns** | **0.0095** |     **160 B** |
| **Match**  | **ComplexLiteral** | **ASP.NET Core Matcher**   | **219.35 ns** | **0.125 ns** | **0.098 ns** |      **-** |         **-** |
| **Match**  | **ComplexLiteral** | **NanoRoute Match Cursor** | **252.64 ns** | **0.356 ns** | **0.297 ns** | **0.0081** |     **136 B** |
| **Match**  | **ComplexParsed**  | **ASP.NET Core Matcher**   | **344.48 ns** | **0.481 ns** | **0.426 ns** | **0.0114** |     **192 B** |
| **Match**  | **ComplexParsed**  | **NanoRoute Match Cursor** | **319.08 ns** | **0.608 ns** | **0.508 ns** | **0.0124** |     **208 B** |
