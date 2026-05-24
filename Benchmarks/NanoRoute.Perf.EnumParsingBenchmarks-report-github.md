```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4


```
| Method          | Value   | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|---------------- |-------- |----------:|----------:|----------:|------:|----------:|------------:|
| **BclEnumTryParse** | **Get**     | **23.553 ns** | **0.0182 ns** | **0.0170 ns** |  **1.00** |         **-** |          **NA** |
| TryParseFast    | Get     |  4.709 ns | 0.0081 ns | 0.0072 ns |  0.20 |         - |          NA |
|                 |         |           |           |           |       |           |             |
| **BclEnumTryParse** | **GET**     | **24.073 ns** | **0.0276 ns** | **0.0245 ns** |  **1.00** |         **-** |          **NA** |
| TryParseFast    | GET     |  5.021 ns | 0.0118 ns | 0.0104 ns |  0.21 |         - |          NA |
|                 |         |           |           |           |       |           |             |
| **BclEnumTryParse** | **Invalid** | **35.683 ns** | **0.0802 ns** | **0.0670 ns** |  **1.00** |         **-** |          **NA** |
| TryParseFast    | Invalid |  5.285 ns | 0.0099 ns | 0.0093 ns |  0.15 |         - |          NA |
