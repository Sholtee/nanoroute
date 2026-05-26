```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method | Scenario     | Mean      | Error    | StdDev   | Allocated |
|------- |------------- |----------:|---------:|---------:|----------:|
| **Decode** | **Plain**        |  **11.86 ns** | **0.012 ns** | **0.010 ns** |         **-** |
| **Decode** | **Plus**         |  **36.99 ns** | **0.059 ns** | **0.055 ns** |         **-** |
| **Decode** | **AsciiEscaped** |  **63.49 ns** | **0.100 ns** | **0.089 ns** |         **-** |
| **Decode** | **Utf8Escaped**  |  **82.17 ns** | **0.301 ns** | **0.267 ns** |         **-** |
| **Decode** | **Mixed**        | **131.48 ns** | **0.115 ns** | **0.102 ns** |         **-** |
