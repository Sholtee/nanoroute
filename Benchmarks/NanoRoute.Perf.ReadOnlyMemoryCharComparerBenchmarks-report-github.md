```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method                   | Scenario          | Mean       | Error     | StdDev     | Median     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |------------------ |-----------:|----------:|-----------:|-----------:|------:|--------:|----------:|------------:|
| **FrameworkComputeHashCode** | **AsciiEqual**        | **11.7684 ns** | **0.2675 ns** |  **0.7001 ns** | **11.4720 ns** |  **1.00** |    **0.08** |         **-** |          **NA** |
| ComputeHashCode          | AsciiEqual        | 21.6722 ns | 0.3838 ns |  0.3402 ns | 21.6712 ns |  1.85 |    0.11 |         - |          NA |
|                          |                   |            |           |            |            |       |         |           |             |
| **FrameworkComputeHashCode** | **AsciiDifferent**    |  **9.8584 ns** | **0.1974 ns** |  **0.3509 ns** |  **9.7695 ns** |  **1.00** |    **0.05** |         **-** |          **NA** |
| ComputeHashCode          | AsciiDifferent    | 23.6839 ns | 0.5068 ns |  1.2045 ns | 23.9682 ns |  2.41 |    0.15 |         - |          NA |
|                          |                   |            |           |            |            |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiEqual**     | **19.7058 ns** | **0.6760 ns** |  **1.9932 ns** | **19.0968 ns** |  **1.01** |    **0.14** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiEqual     | 48.5221 ns | 0.1542 ns |  0.1443 ns | 48.5730 ns |  2.49 |    0.23 |         - |          NA |
|                          |                   |            |           |            |            |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiDifferent** | **24.2463 ns** | **0.0525 ns** |  **0.0491 ns** | **24.2409 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiDifferent | 29.4706 ns | 0.5446 ns |  0.8317 ns | 29.1869 ns |  1.22 |    0.03 |         - |          NA |
|                          |                   |            |           |            |            |       |         |           |             |
| **FrameworkEquals**          | **AsciiEqual**        | **19.9639 ns** | **0.3633 ns** |  **0.3033 ns** | **19.8282 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| Equals                   | AsciiEqual        | 18.9526 ns | 0.4219 ns |  0.5633 ns | 18.8478 ns |  0.95 |    0.03 |         - |          NA |
|                          |                   |            |           |            |            |       |         |           |             |
| **FrameworkEquals**          | **AsciiDifferent**    |  **0.0000 ns** | **0.0000 ns** |  **0.0000 ns** |  **0.0000 ns** |     **?** |       **?** |         **-** |           **?** |
| Equals                   | AsciiDifferent    |  3.8567 ns | 0.0222 ns |  0.0186 ns |  3.8490 ns |     ? |       ? |         - |           ? |
|                          |                   |            |           |            |            |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiEqual**     | **14.8643 ns** | **1.6210 ns** |  **4.7796 ns** | **11.4975 ns** |  **1.09** |    **0.46** |         **-** |          **NA** |
| Equals                   | NonAsciiEqual     | 52.8822 ns | 4.9638 ns | 14.6358 ns | 41.7618 ns |  3.88 |    1.49 |         - |          NA |
|                          |                   |            |           |            |            |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiDifferent** |  **1.2187 ns** | **0.2816 ns** |  **0.8302 ns** |  **0.5140 ns** |  **1.65** |    **1.63** |         **-** |          **NA** |
| Equals                   | NonAsciiDifferent |  3.2091 ns | 0.0253 ns |  0.0211 ns |  3.2023 ns |  4.34 |    2.59 |         - |          NA |
