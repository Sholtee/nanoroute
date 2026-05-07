```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32690/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method                | Mean       | Error     | StdDev    |
|---------------------- |-----------:|----------:|----------:|
| StringKeyedDictionary | 13.2510 ns | 0.0892 ns | 0.0835 ns |
| VerbKeyedDictionary   |  0.3376 ns | 0.0844 ns | 0.0748 ns |
