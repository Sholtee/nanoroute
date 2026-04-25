```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method | Scenario     | Mean      | Error    | StdDev   | Allocated |
|------- |------------- |----------:|---------:|---------:|----------:|
| **Decode** | **Plain**        |  **14.25 ns** | **0.030 ns** | **0.023 ns** |         **-** |
| **Decode** | **Plus**         |  **40.61 ns** | **0.282 ns** | **0.250 ns** |         **-** |
| **Decode** | **AsciiEscaped** |  **73.07 ns** | **0.661 ns** | **0.586 ns** |         **-** |
| **Decode** | **Utf8Escaped**  |  **89.09 ns** | **0.514 ns** | **0.455 ns** |         **-** |
| **Decode** | **Mixed**        | **140.02 ns** | **0.605 ns** | **0.505 ns** |         **-** |
