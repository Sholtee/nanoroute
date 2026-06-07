```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method          | Value   | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|---------------- |-------- |----------:|----------:|----------:|------:|----------:|------------:|
| **BclEnumTryParse** | **Get**     | **31.358 ns** | **0.0165 ns** | **0.0146 ns** |  **1.00** |         **-** |          **NA** |
| TryParseFast    | Get     |  6.519 ns | 0.0342 ns | 0.0267 ns |  0.21 |         - |          NA |
|                 |         |           |           |           |       |           |             |
| **BclEnumTryParse** | **GET**     | **31.149 ns** | **0.0163 ns** | **0.0152 ns** |  **1.00** |         **-** |          **NA** |
| TryParseFast    | GET     |  7.113 ns | 0.0067 ns | 0.0059 ns |  0.23 |         - |          NA |
|                 |         |           |           |           |       |           |             |
| **BclEnumTryParse** | **Invalid** | **44.841 ns** | **0.0490 ns** | **0.0383 ns** |  **1.00** |         **-** |          **NA** |
| TryParseFast    | Invalid |  6.037 ns | 0.0085 ns | 0.0080 ns |  0.13 |         - |          NA |
