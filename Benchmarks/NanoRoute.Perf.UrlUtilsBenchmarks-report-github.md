```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method | Scenario     | Mean      | Error     | StdDev    | Median    | Gen0   | Allocated |
|------- |------------- |----------:|----------:|----------:|----------:|-------:|----------:|
| **Decode** | **Plain**        |  **11.94 ns** |  **0.042 ns** |  **0.035 ns** |  **11.93 ns** |      **-** |         **-** |
| **Decode** | **Plus**         |  **58.16 ns** |  **1.179 ns** |  **1.102 ns** |  **58.64 ns** |      **-** |         **-** |
| **Decode** | **AsciiEscaped** |  **75.70 ns** |  **0.781 ns** |  **0.652 ns** |  **75.44 ns** | **0.0076** |     **128 B** |
| **Decode** | **Utf8Escaped**  |  **91.73 ns** |  **0.404 ns** |  **0.338 ns** |  **91.70 ns** | **0.0076** |     **128 B** |
| **Decode** | **Mixed**        | **181.80 ns** | **15.164 ns** | **44.711 ns** | **153.40 ns** | **0.0153** |     **256 B** |
