```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4


```
| Method                 | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------- |----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| InstanceMethodGroup    | 10.299 ns | 0.1835 ns | 0.1627 ns |  4.16 |    0.09 | 0.0025 |      64 B |          NA |
| CachedInstanceDelegate |  2.478 ns | 0.0418 ns | 0.0391 ns |  1.00 |    0.02 |      - |         - |          NA |
| StaticMethodGroup      |  3.036 ns | 0.0396 ns | 0.0371 ns |  1.23 |    0.02 |      - |         - |          NA |
