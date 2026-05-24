```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4


```
| Method                   | Scenario          | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |------------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **FrameworkComputeHashCode** | **AsciiEqual**        |  **9.2732 ns** | **0.0146 ns** | **0.0129 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | AsciiEqual        | 14.8810 ns | 0.0117 ns | 0.0110 ns |  1.60 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **AsciiDifferent**    |  **9.2745 ns** | **0.0108 ns** | **0.0090 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | AsciiDifferent    | 14.9016 ns | 0.0192 ns | 0.0170 ns |  1.61 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiEqual**     | **12.8329 ns** | **0.0144 ns** | **0.0135 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiEqual     | 15.3535 ns | 0.0102 ns | 0.0085 ns |  1.20 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiDifferent** | **12.8454 ns** | **0.0129 ns** | **0.0107 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiDifferent | 15.3984 ns | 0.0204 ns | 0.0181 ns |  1.20 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **AsciiEqual**        | **15.1639 ns** | **0.0480 ns** | **0.0401 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Equals                   | AsciiEqual        | 13.3946 ns | 0.0377 ns | 0.0352 ns |  0.88 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **AsciiDifferent**    |  **0.5920 ns** | **0.0023 ns** | **0.0022 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Equals                   | AsciiDifferent    |  2.8556 ns | 0.0057 ns | 0.0050 ns |  4.82 |    0.02 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiEqual**     | **11.0100 ns** | **0.0594 ns** | **0.0556 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Equals                   | NonAsciiEqual     | 15.1045 ns | 0.0140 ns | 0.0125 ns |  1.37 |    0.01 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiDifferent** |  **0.5909 ns** | **0.0022 ns** | **0.0020 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Equals                   | NonAsciiDifferent |  3.1189 ns | 0.0096 ns | 0.0085 ns |  5.28 |    0.02 |         - |          NA |
