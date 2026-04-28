```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method        | Path                   | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------- |----------------------- |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| WalkSegments  | /api/users/42/orders/7 |  33.30 ns | 0.156 ns | 0.139 ns |  0.27 |    0.01 |      - |         - |        0.00 |
| SplitSegments | /api/users/42/orders/7 | 123.76 ns | 2.467 ns | 3.616 ns |  1.00 |    0.04 | 0.0129 |     328 B |        1.00 |
