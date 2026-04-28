```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method                   | Scenario          | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |------------------ |-----------:|----------:|----------:|-----------:|------:|--------:|----------:|------------:|
| **FrameworkComputeHashCode** | **AsciiEqual**        |  **9.3601 ns** | **0.0669 ns** | **0.0559 ns** |  **9.3422 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| ComputeHashCode          | AsciiEqual        | 16.0102 ns | 0.0952 ns | 0.0795 ns | 15.9867 ns |  1.71 |    0.01 |         - |          NA |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkComputeHashCode** | **AsciiDifferent**    | **11.3123 ns** | **0.6237 ns** | **1.8390 ns** | **10.6729 ns** |  **1.02** |    **0.22** |         **-** |          **NA** |
| ComputeHashCode          | AsciiDifferent    | 16.6871 ns | 0.1806 ns | 0.1508 ns | 16.7144 ns |  1.51 |    0.22 |         - |          NA |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiEqual**     | **13.7797 ns** | **0.1807 ns** | **0.1601 ns** | **13.7177 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiEqual     | 23.9061 ns | 0.4226 ns | 0.3299 ns | 23.8172 ns |  1.74 |    0.03 |         - |          NA |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiDifferent** | **13.5522 ns** | **0.2827 ns** | **0.2207 ns** | **13.5123 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiDifferent | 24.3250 ns | 0.4979 ns | 0.6980 ns | 24.0056 ns |  1.80 |    0.06 |         - |          NA |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkEquals**          | **AsciiEqual**        | **16.5331 ns** | **0.3694 ns** | **0.4672 ns** | **16.4412 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| Equals                   | AsciiEqual        | 25.2057 ns | 0.5470 ns | 1.2233 ns | 25.2597 ns |  1.53 |    0.08 |         - |          NA |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkEquals**          | **AsciiDifferent**    |  **1.9270 ns** | **0.0260 ns** | **0.0243 ns** |  **1.9372 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| Equals                   | AsciiDifferent    |  6.8613 ns | 0.0773 ns | 0.0724 ns |  6.8779 ns |  3.56 |    0.06 |         - |          NA |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiEqual**     | **24.8542 ns** | **0.3943 ns** | **0.3688 ns** | **24.7667 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| Equals                   | NonAsciiEqual     | 43.9956 ns | 1.6906 ns | 4.8233 ns | 42.9973 ns |  1.77 |    0.19 |         - |          NA |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiDifferent** |  **0.5886 ns** | **0.0338 ns** | **0.0390 ns** |  **0.5719 ns** |  **1.00** |    **0.09** |         **-** |          **NA** |
| Equals                   | NonAsciiDifferent |  4.0066 ns | 0.5546 ns | 1.6353 ns |  2.8411 ns |  6.83 |    2.81 |         - |          NA |
