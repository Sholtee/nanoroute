```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32690/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method   | Value   | Mean     | Error    | StdDev   |
|--------- |-------- |---------:|---------:|---------:|
| **TryParse** | **Get**     | **24.62 ns** | **0.217 ns** | **0.203 ns** |
| **TryParse** | **Invalid** | **37.58 ns** | **0.414 ns** | **0.387 ns** |
