```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method | ScenarioKind   | RouteMatcherFactory    | Mean      | Error    | StdDev   | Gen0   | Allocated |
|------- |--------------- |----------------------- |----------:|---------:|---------:|-------:|----------:|
| **Match**  | **SingleLiteral**  | **ASP.NET Core Matcher**   | **142.71 ns** | **0.192 ns** | **0.160 ns** |      **-** |         **-** |
| **Match**  | **SingleLiteral**  | **NanoRoute Match Cursor** |  **81.24 ns** | **0.278 ns** | **0.246 ns** | **0.0076** |     **128 B** |
| **Match**  | **SingleParsed**   | **ASP.NET Core Matcher**   | **170.34 ns** | **0.561 ns** | **0.525 ns** | **0.0067** |     **112 B** |
| **Match**  | **SingleParsed**   | **NanoRoute Match Cursor** |  **92.90 ns** | **0.276 ns** | **0.230 ns** | **0.0091** |     **152 B** |
| **Match**  | **ComplexLiteral** | **ASP.NET Core Matcher**   | **219.49 ns** | **0.113 ns** | **0.095 ns** |      **-** |         **-** |
| **Match**  | **ComplexLiteral** | **NanoRoute Match Cursor** | **247.42 ns** | **0.313 ns** | **0.293 ns** | **0.0076** |     **128 B** |
| **Match**  | **ComplexParsed**  | **ASP.NET Core Matcher**   | **347.12 ns** | **3.348 ns** | **3.131 ns** | **0.0114** |     **192 B** |
| **Match**  | **ComplexParsed**  | **NanoRoute Match Cursor** | **322.30 ns** | **1.312 ns** | **1.227 ns** | **0.0119** |     **200 B** |
