```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4


```
| Method        | Path                   | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------- |----------------------- |----------:|---------:|---------:|------:|-------:|----------:|------------:|
| WalkSegments  | /api/users/42/orders/7 |  32.64 ns | 0.040 ns | 0.034 ns |  0.30 |      - |         - |        0.00 |
| SplitSegments | /api/users/42/orders/7 | 109.64 ns | 0.859 ns | 0.804 ns |  1.00 | 0.0130 |     328 B |        1.00 |
