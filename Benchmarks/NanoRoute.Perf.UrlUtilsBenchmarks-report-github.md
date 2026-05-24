```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4


```
| Method | Scenario     | Mean      | Error    | StdDev   | Allocated |
|------- |------------- |----------:|---------:|---------:|----------:|
| **Decode** | **Plain**        |  **11.92 ns** | **0.262 ns** | **0.269 ns** |         **-** |
| **Decode** | **Plus**         |  **44.86 ns** | **0.482 ns** | **0.427 ns** |         **-** |
| **Decode** | **AsciiEscaped** |  **62.21 ns** | **0.449 ns** | **0.398 ns** |         **-** |
| **Decode** | **Utf8Escaped**  |  **92.46 ns** | **0.080 ns** | **0.071 ns** |         **-** |
| **Decode** | **Mixed**        | **129.96 ns** | **0.128 ns** | **0.107 ns** |         **-** |
