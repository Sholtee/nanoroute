```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method                 | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------- |---------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| InstanceMethodGroup    | 9.090 ns | 0.1867 ns | 0.1746 ns |  3.65 |    0.08 | 0.0038 |      64 B |          NA |
| CachedInstanceDelegate | 2.494 ns | 0.0333 ns | 0.0260 ns |  1.00 |    0.01 |      - |         - |          NA |
| StaticMethodGroup      | 3.192 ns | 0.0059 ns | 0.0052 ns |  1.28 |    0.01 |      - |         - |          NA |
