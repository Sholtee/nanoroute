```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method                | Scenario       | Mean      | Error     | StdDev     | Median    | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------------------- |--------------- |----------:|----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **ParseWithValueParsers** | **Int**            | **18.382 ns** | **0.4088 ns** |  **0.5731 ns** | **18.655 ns** |  **1.13** |    **0.04** |         **-** |          **NA** |
| ParseWithFramework    | Int            | 16.266 ns | 0.1012 ns |  0.0845 ns | 16.270 ns |  1.00 |    0.01 |         - |          NA |
|                       |                |           |           |            |           |       |         |           |             |
| **ParseWithValueParsers** | **Boolean**        |  **5.490 ns** | **0.0399 ns** |  **0.0354 ns** |  **5.482 ns** |  **2.26** |    **0.80** |         **-** |          **NA** |
| ParseWithFramework    | Boolean        |  2.940 ns | 0.4958 ns |  1.4618 ns |  1.943 ns |  1.21 |    0.77 |         - |          NA |
|                       |                |           |           |            |           |       |         |           |             |
| **ParseWithValueParsers** | **GuidHyphenated** | **66.857 ns** | **7.8595 ns** | **23.1739 ns** | **48.344 ns** |  **2.58** |    **0.89** |         **-** |          **NA** |
| ParseWithFramework    | GuidHyphenated | 25.940 ns | 0.4733 ns |  0.8773 ns | 25.680 ns |  1.00 |    0.04 |         - |          NA |
|                       |                |           |           |            |           |       |         |           |             |
| **ParseWithValueParsers** | **GuidCompact**    | **47.505 ns** | **0.7216 ns** |  **0.6026 ns** | **47.275 ns** |  **1.24** |    **0.02** |         **-** |          **NA** |
| ParseWithFramework    | GuidCompact    | 38.185 ns | 0.5777 ns |  0.5404 ns | 38.464 ns |  1.00 |    0.02 |         - |          NA |
