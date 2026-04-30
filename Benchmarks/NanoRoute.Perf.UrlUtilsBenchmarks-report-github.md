```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method | Scenario     | Mean      | Error    | StdDev   | Allocated |
|------- |------------- |----------:|---------:|---------:|----------:|
| **Decode** | **Plain**        |  **10.47 ns** | **0.021 ns** | **0.018 ns** |         **-** |
| **Decode** | **Plus**         |  **37.46 ns** | **0.480 ns** | **0.449 ns** |         **-** |
| **Decode** | **AsciiEscaped** |  **62.87 ns** | **0.039 ns** | **0.036 ns** |         **-** |
| **Decode** | **Utf8Escaped**  |  **78.72 ns** | **0.350 ns** | **0.327 ns** |         **-** |
| **Decode** | **Mixed**        | **120.51 ns** | **1.464 ns** | **1.222 ns** |         **-** |
