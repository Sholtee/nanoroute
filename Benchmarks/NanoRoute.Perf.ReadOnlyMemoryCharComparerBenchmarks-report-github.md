```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32690/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method                   | Scenario          | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |------------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **FrameworkComputeHashCode** | **AsciiEqual**        |  **9.3940 ns** | **0.0648 ns** | **0.0607 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| ComputeHashCode          | AsciiEqual        | 15.0138 ns | 0.1543 ns | 0.1443 ns |  1.60 |    0.02 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **AsciiDifferent**    |  **9.4840 ns** | **0.0763 ns** | **0.0714 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| ComputeHashCode          | AsciiDifferent    | 15.0905 ns | 0.1099 ns | 0.0918 ns |  1.59 |    0.01 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiEqual**     | **12.9657 ns** | **0.1227 ns** | **0.1147 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiEqual     | 15.5696 ns | 0.1337 ns | 0.1186 ns |  1.20 |    0.01 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiDifferent** | **12.9603 ns** | **0.1418 ns** | **0.1327 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiDifferent | 16.1780 ns | 0.1825 ns | 0.1617 ns |  1.25 |    0.02 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **AsciiEqual**        | **15.3683 ns** | **0.1218 ns** | **0.1140 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Equals                   | AsciiEqual        | 13.6420 ns | 0.1234 ns | 0.1155 ns |  0.89 |    0.01 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **AsciiDifferent**    |  **0.6444 ns** | **0.0209 ns** | **0.0186 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| Equals                   | AsciiDifferent    |  2.9186 ns | 0.0244 ns | 0.0203 ns |  4.53 |    0.13 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiEqual**     | **11.2526 ns** | **0.0812 ns** | **0.0760 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Equals                   | NonAsciiEqual     | 15.0874 ns | 0.1187 ns | 0.1052 ns |  1.34 |    0.01 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiDifferent** |  **0.5903 ns** | **0.0193 ns** | **0.0171 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| Equals                   | NonAsciiDifferent |  2.8779 ns | 0.0150 ns | 0.0126 ns |  4.88 |    0.13 |         - |          NA |
