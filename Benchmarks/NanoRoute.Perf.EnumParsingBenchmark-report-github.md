```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method   | Value   | Mean     | Error    | StdDev   |
|--------- |-------- |---------:|---------:|---------:|
| **TryParse** | **Get**     | **23.99 ns** | **0.009 ns** | **0.008 ns** |
| **TryParse** | **Invalid** | **35.07 ns** | **0.032 ns** | **0.025 ns** |
