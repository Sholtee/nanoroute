```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method | Scenario     | Mean      | Error     | StdDev    | Median    | Allocated |
|------- |------------- |----------:|----------:|----------:|----------:|----------:|
| **Decode** | **Plain**        |  **11.56 ns** |  **0.226 ns** |  **0.365 ns** |  **11.46 ns** |         **-** |
| **Decode** | **Plus**         |  **59.91 ns** |  **0.824 ns** |  **0.770 ns** |  **59.84 ns** |         **-** |
| **Decode** | **AsciiEscaped** |  **62.71 ns** |  **0.809 ns** |  **1.374 ns** |  **62.32 ns** |         **-** |
| **Decode** | **Utf8Escaped**  |  **93.95 ns** |  **0.351 ns** |  **0.293 ns** |  **93.88 ns** |         **-** |
| **Decode** | **Mixed**        | **165.10 ns** | **15.131 ns** | **44.613 ns** | **138.93 ns** |         **-** |
