```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method        | Path                   | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------- |----------------------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| WalkSegments  | /api/users/42/orders/7 | 33.24 ns | 0.257 ns | 0.240 ns |  0.33 |    0.01 |      - |         - |        0.00 |
| SplitSegments | /api/users/42/orders/7 | 99.98 ns | 1.894 ns | 1.860 ns |  1.00 |    0.03 | 0.0196 |     328 B |        1.00 |
