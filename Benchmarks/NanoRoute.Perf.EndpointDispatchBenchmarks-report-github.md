```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method   | ScenarioKind   | EndpointDispatcherFactory | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------- |--------------- |-------------------------- |---------:|--------:|--------:|-------:|----------:|
| **Dispatch** | **SingleLiteral**  | **ASP.NET Core Minimal API**  | **143.8 ns** | **0.20 ns** | **0.18 ns** |      **-** |         **-** |
| **Dispatch** | **SingleLiteral**  | **NanoRoute Router**          | **147.0 ns** | **0.46 ns** | **0.43 ns** | **0.0277** |     **464 B** |
| **Dispatch** | **SingleParsed**   | **ASP.NET Core Minimal API**  | **222.7 ns** | **0.42 ns** | **0.37 ns** | **0.0067** |     **112 B** |
| **Dispatch** | **SingleParsed**   | **NanoRoute Router**          | **172.4 ns** | **1.82 ns** | **1.61 ns** | **0.0372** |     **624 B** |
| **Dispatch** | **ComplexLiteral** | **ASP.NET Core Minimal API**  | **259.6 ns** | **0.26 ns** | **0.22 ns** |      **-** |         **-** |
| **Dispatch** | **ComplexLiteral** | **NanoRoute Router**          | **320.3 ns** | **2.20 ns** | **2.06 ns** | **0.0277** |     **464 B** |
| **Dispatch** | **ComplexParsed**  | **ASP.NET Core Minimal API**  | **426.7 ns** | **2.38 ns** | **1.98 ns** | **0.0114** |     **192 B** |
| **Dispatch** | **ComplexParsed**  | **NanoRoute Router**          | **396.1 ns** | **1.90 ns** | **1.58 ns** | **0.0401** |     **672 B** |
