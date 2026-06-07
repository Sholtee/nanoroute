```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method        | Path                   | Mean     | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------- |----------------------- |---------:|---------:|---------:|------:|-------:|----------:|------------:|
| WalkSegments  | /api/users/42/orders/7 | 32.12 ns | 0.022 ns | 0.019 ns |  0.34 |      - |         - |        0.00 |
| SplitSegments | /api/users/42/orders/7 | 95.15 ns | 0.947 ns | 0.885 ns |  1.00 | 0.0196 |     328 B |        1.00 |
