```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method   | ScenarioKind   | EndpointDispatcherFactory | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------- |--------------- |-------------------------- |---------:|--------:|--------:|-------:|----------:|
| **Dispatch** | **SingleLiteral**  | **ASP.NET Core Minimal API**  | **148.2 ns** | **0.12 ns** | **0.10 ns** |      **-** |         **-** |
| **Dispatch** | **SingleLiteral**  | **NanoRoute Router**          | **174.4 ns** | **0.28 ns** | **0.24 ns** | **0.0391** |     **656 B** |
| **Dispatch** | **SingleParsed**   | **ASP.NET Core Minimal API**  | **250.3 ns** | **0.57 ns** | **0.51 ns** | **0.0067** |     **112 B** |
| **Dispatch** | **SingleParsed**   | **NanoRoute Router**          | **160.8 ns** | **0.45 ns** | **0.42 ns** | **0.0405** |     **680 B** |
| **Dispatch** | **ComplexLiteral** | **ASP.NET Core Minimal API**  | **257.7 ns** | **0.23 ns** | **0.20 ns** |      **-** |         **-** |
| **Dispatch** | **ComplexLiteral** | **NanoRoute Router**          | **395.1 ns** | **1.44 ns** | **1.21 ns** | **0.0391** |     **656 B** |
| **Dispatch** | **ComplexParsed**  | **ASP.NET Core Minimal API**  | **411.4 ns** | **1.64 ns** | **1.54 ns** | **0.0114** |     **192 B** |
| **Dispatch** | **ComplexParsed**  | **NanoRoute Router**          | **446.7 ns** | **2.69 ns** | **2.38 ns** | **0.0434** |     **728 B** |
