```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method        | Path                   | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------- |----------------------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| WalkSegments  | /api/users/42/orders/7 | 35.89 ns | 0.114 ns | 0.095 ns |  0.36 |    0.00 |      - |         - |        0.00 |
| SplitSegments | /api/users/42/orders/7 | 98.83 ns | 1.301 ns | 1.153 ns |  1.00 |    0.02 | 0.0196 |     328 B |        1.00 |
