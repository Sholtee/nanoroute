```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method   | Value   | Mean     | Error    | StdDev   |
|--------- |-------- |---------:|---------:|---------:|
| **TryParse** | **Get**     | **31.16 ns** | **0.646 ns** | **1.096 ns** |
| **TryParse** | **Invalid** | **42.38 ns** | **0.292 ns** | **0.273 ns** |
