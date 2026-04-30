```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method        | Path                   | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------- |----------------------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| WalkSegments  | /api/users/42/orders/7 | 26.06 ns | 0.061 ns | 0.048 ns |  0.32 |    0.01 |      - |         - |        0.00 |
| SplitSegments | /api/users/42/orders/7 | 80.49 ns | 1.481 ns | 1.585 ns |  1.00 |    0.03 | 0.0196 |     328 B |        1.00 |
