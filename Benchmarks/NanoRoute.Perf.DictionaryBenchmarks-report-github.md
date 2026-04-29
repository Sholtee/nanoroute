```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method                | Mean       | Error     | StdDev    |
|---------------------- |-----------:|----------:|----------:|
| StringKeyedDictionary | 16.8950 ns | 0.1705 ns | 0.1424 ns |
| VerbKeyedDictionary   |  0.3383 ns | 0.0704 ns | 0.0723 ns |
