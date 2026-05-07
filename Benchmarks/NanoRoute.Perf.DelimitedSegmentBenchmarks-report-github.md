```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32690/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method        | Path                   | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------- |----------------------- |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| WalkSegments  | /api/users/42/orders/7 |  32.87 ns | 0.192 ns | 0.170 ns |  0.29 |    0.00 |      - |         - |        0.00 |
| SplitSegments | /api/users/42/orders/7 | 111.44 ns | 1.572 ns | 1.471 ns |  1.00 |    0.02 | 0.0130 |     328 B |        1.00 |
