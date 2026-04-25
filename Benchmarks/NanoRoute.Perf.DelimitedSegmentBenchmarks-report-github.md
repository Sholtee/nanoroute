```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method        | Path                   | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------- |----------------------- |----------:|---------:|---------:|------:|-------:|----------:|------------:|
| WalkSegments  | /api/users/42/orders/7 |  32.14 ns | 0.120 ns | 0.100 ns |  0.31 |      - |         - |        0.00 |
| SplitSegments | /api/users/42/orders/7 | 103.11 ns | 1.095 ns | 1.024 ns |  1.00 | 0.0196 |     328 B |        1.00 |
