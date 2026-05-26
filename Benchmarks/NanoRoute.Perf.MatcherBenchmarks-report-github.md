```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method | ScenarioKind   | RouteMatcherFactory    | Mean     | Error   | StdDev  | Gen0   | Allocated |
|------- |--------------- |----------------------- |---------:|--------:|--------:|-------:|----------:|
| **Match**  | **SingleLiteral**  | **ASP.NET Core Matcher**   | **115.9 ns** | **0.13 ns** | **0.12 ns** |      **-** |         **-** |
| **Match**  | **SingleLiteral**  | **NanoRoute Match Cursor** | **120.8 ns** | **0.71 ns** | **0.63 ns** | **0.0300** |     **504 B** |
| **Match**  | **SingleParsed**   | **ASP.NET Core Matcher**   | **172.2 ns** | **0.24 ns** | **0.23 ns** | **0.0067** |     **112 B** |
| **Match**  | **SingleParsed**   | **NanoRoute Match Cursor** | **133.3 ns** | **0.57 ns** | **0.53 ns** | **0.0315** |     **528 B** |
| **Match**  | **ComplexLiteral** | **ASP.NET Core Matcher**   | **217.5 ns** | **0.20 ns** | **0.17 ns** |      **-** |         **-** |
| **Match**  | **ComplexLiteral** | **NanoRoute Match Cursor** | **336.4 ns** | **0.58 ns** | **0.55 ns** | **0.0300** |     **504 B** |
| **Match**  | **ComplexParsed**  | **ASP.NET Core Matcher**   | **359.3 ns** | **0.64 ns** | **0.59 ns** | **0.0114** |     **192 B** |
| **Match**  | **ComplexParsed**  | **NanoRoute Match Cursor** | **422.3 ns** | **0.96 ns** | **0.81 ns** | **0.0343** |     **576 B** |
