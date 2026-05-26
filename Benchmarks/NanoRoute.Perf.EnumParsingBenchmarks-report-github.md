```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method          | Value   | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|---------------- |-------- |----------:|----------:|----------:|------:|----------:|------------:|
| **BclEnumTryParse** | **Get**     | **29.796 ns** | **0.0235 ns** | **0.0183 ns** |  **1.00** |         **-** |          **NA** |
| TryParseFast    | Get     |  6.122 ns | 0.0084 ns | 0.0074 ns |  0.21 |         - |          NA |
|                 |         |           |           |           |       |           |             |
| **BclEnumTryParse** | **GET**     | **29.194 ns** | **0.0355 ns** | **0.0297 ns** |  **1.00** |         **-** |          **NA** |
| TryParseFast    | GET     |  6.559 ns | 0.0090 ns | 0.0080 ns |  0.22 |         - |          NA |
|                 |         |           |           |           |       |           |             |
| **BclEnumTryParse** | **Invalid** | **42.836 ns** | **0.0310 ns** | **0.0242 ns** |  **1.00** |         **-** |          **NA** |
| TryParseFast    | Invalid |  6.033 ns | 0.0041 ns | 0.0032 ns |  0.14 |         - |          NA |
