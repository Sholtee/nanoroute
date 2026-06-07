```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method          | HandlerCount | Mean     | Error    | StdDev   | Allocated |
|---------------- |------------- |---------:|---------:|---------:|----------:|
| **TryEmitHandlers** | **0**            | **10.59 ns** | **0.021 ns** | **0.018 ns** |         **-** |
| **TryEmitHandlers** | **1**            | **14.97 ns** | **0.032 ns** | **0.025 ns** |         **-** |
| **TryEmitHandlers** | **5**            | **33.28 ns** | **0.062 ns** | **0.055 ns** |         **-** |
