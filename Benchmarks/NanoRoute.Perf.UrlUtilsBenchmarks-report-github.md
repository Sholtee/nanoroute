```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method | Scenario     | Mean      | Error    | StdDev   | Allocated |
|------- |------------- |----------:|---------:|---------:|----------:|
| **Decode** | **Plain**        |  **14.11 ns** | **0.013 ns** | **0.012 ns** |         **-** |
| **Decode** | **Plus**         |  **40.05 ns** | **0.192 ns** | **0.161 ns** |         **-** |
| **Decode** | **AsciiEscaped** |  **69.83 ns** | **0.071 ns** | **0.059 ns** |         **-** |
| **Decode** | **Utf8Escaped**  |  **87.25 ns** | **0.143 ns** | **0.119 ns** |         **-** |
| **Decode** | **Mixed**        | **139.16 ns** | **0.247 ns** | **0.219 ns** |         **-** |
