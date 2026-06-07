```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method                   | Scenario          | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |------------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **FrameworkComputeHashCode** | **AsciiEqual**        | **10.1359 ns** | **0.0158 ns** | **0.0132 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | AsciiEqual        | 22.2394 ns | 0.0175 ns | 0.0136 ns |  2.19 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **AsciiDifferent**    | **10.1280 ns** | **0.0124 ns** | **0.0103 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | AsciiDifferent    | 22.1340 ns | 0.0249 ns | 0.0233 ns |  2.19 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiEqual**     | **15.7690 ns** | **0.0124 ns** | **0.0103 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiEqual     | 19.9662 ns | 0.0559 ns | 0.0467 ns |  1.27 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiDifferent** | **15.7478 ns** | **0.0098 ns** | **0.0087 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiDifferent | 19.9327 ns | 0.0121 ns | 0.0101 ns |  1.27 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **AsciiEqual**        | **19.4472 ns** | **0.0249 ns** | **0.0194 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Equals                   | AsciiEqual        | 15.4037 ns | 0.0097 ns | 0.0081 ns |  0.79 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **AsciiDifferent**    |  **0.7120 ns** | **0.0025 ns** | **0.0022 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Equals                   | AsciiDifferent    |  6.3706 ns | 0.0072 ns | 0.0064 ns |  8.95 |    0.03 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiEqual**     | **11.3339 ns** | **0.0168 ns** | **0.0131 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Equals                   | NonAsciiEqual     | 20.6475 ns | 0.0348 ns | 0.0290 ns |  1.82 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiDifferent** |  **0.7124 ns** | **0.0021 ns** | **0.0019 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Equals                   | NonAsciiDifferent |  6.3651 ns | 0.0119 ns | 0.0106 ns |  8.94 |    0.03 |         - |          NA |
