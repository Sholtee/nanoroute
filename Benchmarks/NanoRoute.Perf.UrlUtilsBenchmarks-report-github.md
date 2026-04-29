```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method | Scenario     | Mean      | Error    | StdDev    | Median    | Allocated |
|------- |------------- |----------:|---------:|----------:|----------:|----------:|
| **Decode** | **Plain**        |  **11.94 ns** | **0.054 ns** |  **0.045 ns** |  **11.93 ns** |         **-** |
| **Decode** | **Plus**         |  **45.61 ns** | **3.462 ns** | **10.207 ns** |  **37.18 ns** |         **-** |
| **Decode** | **AsciiEscaped** |  **64.52 ns** | **0.289 ns** |  **0.241 ns** |  **64.45 ns** |         **-** |
| **Decode** | **Utf8Escaped**  |  **81.85 ns** | **1.049 ns** |  **1.401 ns** |  **81.28 ns** |         **-** |
| **Decode** | **Mixed**        | **131.95 ns** | **0.600 ns** |  **0.501 ns** | **131.78 ns** |         **-** |
