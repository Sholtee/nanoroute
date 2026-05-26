```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method                   | Scenario          | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |------------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **FrameworkComputeHashCode** | **AsciiEqual**        |  **8.9944 ns** | **0.0043 ns** | **0.0036 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | AsciiEqual        | 22.6180 ns | 0.0431 ns | 0.0360 ns |  2.51 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **AsciiDifferent**    |  **8.9449 ns** | **0.0073 ns** | **0.0061 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | AsciiDifferent    | 22.6184 ns | 0.0388 ns | 0.0324 ns |  2.53 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiEqual**     | **16.1660 ns** | **0.0145 ns** | **0.0128 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiEqual     | 17.4754 ns | 0.0077 ns | 0.0068 ns |  1.08 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiDifferent** | **16.1205 ns** | **0.0188 ns** | **0.0167 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiDifferent | 19.6462 ns | 0.0135 ns | 0.0120 ns |  1.22 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **AsciiEqual**        | **19.6537 ns** | **0.0184 ns** | **0.0163 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Equals                   | AsciiEqual        | 15.0458 ns | 0.0112 ns | 0.0093 ns |  0.77 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **AsciiDifferent**    |  **0.4650 ns** | **0.0037 ns** | **0.0032 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Equals                   | AsciiDifferent    |  4.3215 ns | 0.0192 ns | 0.0160 ns |  9.29 |    0.07 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiEqual**     | **11.3529 ns** | **0.0170 ns** | **0.0150 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Equals                   | NonAsciiEqual     | 19.4562 ns | 0.0198 ns | 0.0165 ns |  1.71 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiDifferent** |  **0.4664 ns** | **0.0024 ns** | **0.0020 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Equals                   | NonAsciiDifferent |  4.3906 ns | 0.0173 ns | 0.0153 ns |  9.41 |    0.05 |         - |          NA |
