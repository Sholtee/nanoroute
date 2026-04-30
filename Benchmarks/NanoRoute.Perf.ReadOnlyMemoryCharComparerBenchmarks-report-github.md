```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method                   | Scenario          | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |------------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **FrameworkComputeHashCode** | **AsciiEqual**        |  **7.8544 ns** | **0.0055 ns** | **0.0049 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | AsciiEqual        | 15.7019 ns | 0.0143 ns | 0.0127 ns |  2.00 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **AsciiDifferent**    |  **7.8549 ns** | **0.0359 ns** | **0.0280 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | AsciiDifferent    | 15.7841 ns | 0.0112 ns | 0.0099 ns |  2.01 |    0.01 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiEqual**     | **12.2004 ns** | **0.0072 ns** | **0.0060 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiEqual     | 21.1836 ns | 0.0307 ns | 0.0256 ns |  1.74 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkComputeHashCode** | **NonAsciiDifferent** | **12.2174 ns** | **0.0294 ns** | **0.0245 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ComputeHashCode          | NonAsciiDifferent | 21.2896 ns | 0.0227 ns | 0.0201 ns |  1.74 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **AsciiEqual**        | **15.3764 ns** | **0.0458 ns** | **0.0383 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Equals                   | AsciiEqual        | 11.9671 ns | 0.0171 ns | 0.0151 ns |  0.78 |    0.00 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **AsciiDifferent**    |  **0.3092 ns** | **0.0344 ns** | **0.0368 ns** |  **1.01** |    **0.16** |         **-** |          **NA** |
| Equals                   | AsciiDifferent    |  4.0278 ns | 0.0043 ns | 0.0033 ns | 13.19 |    1.38 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiEqual**     |  **8.8260 ns** | **0.1107 ns** | **0.0924 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Equals                   | NonAsciiEqual     | 28.2216 ns | 0.1269 ns | 0.1060 ns |  3.20 |    0.03 |         - |          NA |
|                          |                   |            |           |           |       |         |           |             |
| **FrameworkEquals**          | **NonAsciiDifferent** |  **0.2988 ns** | **0.0265 ns** | **0.0248 ns** |  **1.01** |    **0.11** |         **-** |          **NA** |
| Equals                   | NonAsciiDifferent |  4.0264 ns | 0.0023 ns | 0.0020 ns | 13.56 |    1.02 |         - |          NA |
