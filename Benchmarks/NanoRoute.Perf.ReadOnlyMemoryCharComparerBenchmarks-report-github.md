```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method                   | Scenario          | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |------------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **FrameworkComputeHashCode** | **AsciiEqual**        | **10.2855 ns** | **0.1425 ns** | **0.1263 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| ComputeHashCode          | AsciiEqual        | 20.3301 ns | 0.1048 ns | 0.0875 ns |  1.98 |    0.02 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **AsciiDifferent**    | **10.2219 ns** | **0.0352 ns** | **0.0312 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | AsciiDifferent    | 20.7313 ns | 0.1044 ns | 0.0872 ns |  2.03 |    0.01 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiEqual**     | **15.8507 ns** | **0.0274 ns** | **0.0229 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiEqual     | 27.4854 ns | 0.0621 ns | 0.0519 ns |  1.73 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiDifferent** | **15.8376 ns** | **0.0325 ns** | **0.0288 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiDifferent | 27.9385 ns | 0.0855 ns | 0.0667 ns |  1.76 |    0.01 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **AsciiEqual**        | **19.5735 ns** | **0.0714 ns** | **0.0596 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Equals                   | AsciiEqual        | 15.5196 ns | 0.0153 ns | 0.0119 ns |  0.79 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **AsciiDifferent**    |  **0.7225 ns** | **0.0066 ns** | **0.0059 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Equals                   | AsciiDifferent    |  5.5749 ns | 0.0090 ns | 0.0075 ns |  7.72 |    0.06 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiEqual**     | **11.4430 ns** | **0.0357 ns** | **0.0298 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Equals                   | NonAsciiEqual     | 36.6033 ns | 0.1800 ns | 0.1503 ns |  3.20 |    0.01 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiDifferent** |  **0.7201 ns** | **0.0063 ns** | **0.0056 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Equals                   | NonAsciiDifferent |  5.5803 ns | 0.0342 ns | 0.0285 ns |  7.75 |    0.07 |         - |          NA |
