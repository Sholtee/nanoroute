```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method                   | Scenario          | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |------------------ |-----------:|----------:|----------:|-----------:|------:|--------:|----------:|------------:|
| **FrameworkComputeHashCode** | **AsciiEqual**        | **16.1022 ns** | **0.1156 ns** | **0.1081 ns** | **16.1246 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| ComputeHashCode          | AsciiEqual        | 28.9616 ns | 0.5065 ns | 0.9134 ns | 28.9172 ns |  1.80 |    0.06 |         - |          NA |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkComputeHashCode** | **AsciiDifferent**    | **10.3313 ns** | **0.2367 ns** | **0.2430 ns** | **10.3643 ns** |  **1.00** |    **0.03** |         **-** |          **NA** |
| ComputeHashCode          | AsciiDifferent    | 29.6048 ns | 0.6064 ns | 1.4175 ns | 29.4994 ns |  2.87 |    0.15 |         - |          NA |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiEqual**     | **17.9390 ns** | **0.3865 ns** | **0.6018 ns** | **17.9427 ns** |  **1.00** |    **0.05** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiEqual     | 33.5035 ns | 1.3779 ns | 3.7952 ns | 31.5305 ns |  1.87 |    0.22 |         - |          NA |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiDifferent** | **18.5057 ns** | **1.2136 ns** | **3.5784 ns** | **15.5124 ns** |  **1.04** |    **0.28** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiDifferent | 30.2848 ns | 0.5075 ns | 0.8617 ns | 29.9719 ns |  1.70 |    0.31 |         - |          NA |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkEquals**          | **AsciiEqual**        | **19.8518 ns** | **0.0868 ns** | **0.0678 ns** | **19.8437 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Equals                   | AsciiEqual        | 17.7605 ns | 1.3740 ns | 4.0512 ns | 15.0854 ns |  0.89 |    0.20 |         - |          NA |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkEquals**          | **AsciiDifferent**    |  **0.6547 ns** | **0.2831 ns** | **0.8347 ns** |  **0.0000 ns** |     **?** |       **?** |         **-** |           **?** |
| Equals                   | AsciiDifferent    |  3.7868 ns | 0.0339 ns | 0.0766 ns |  3.7651 ns |     ? |       ? |         - |           ? |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiEqual**     | **10.9896 ns** | **0.1921 ns** | **0.3103 ns** | **10.9118 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| Equals                   | NonAsciiEqual     | 38.9067 ns | 0.2073 ns | 0.3348 ns | 38.7930 ns |  3.54 |    0.09 |         - |          NA |
|                          |                   |            |           |           |            |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiDifferent** |  **0.4713 ns** | **0.0082 ns** | **0.0158 ns** |  **0.4672 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| Equals                   | NonAsciiDifferent |  3.8437 ns | 0.1079 ns | 0.1918 ns |  3.7864 ns |  8.16 |    0.47 |         - |          NA |
