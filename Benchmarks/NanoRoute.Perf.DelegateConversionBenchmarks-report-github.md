```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method                 | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------- |---------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| InstanceMethodGroup    | 8.465 ns | 0.0603 ns | 0.0564 ns |  3.00 |    0.02 | 0.0038 |      64 B |          NA |
| CachedInstanceDelegate | 2.822 ns | 0.0038 ns | 0.0032 ns |  1.00 |    0.00 |      - |         - |          NA |
| StaticMethodGroup      | 3.453 ns | 0.0033 ns | 0.0028 ns |  1.22 |    0.00 |      - |         - |          NA |
