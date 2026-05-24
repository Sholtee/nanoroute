```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4


```
| Method                | Mean       | Error     | StdDev    |
|---------------------- |-----------:|----------:|----------:|
| StringKeyedDictionary | 13.2286 ns | 0.0496 ns | 0.0464 ns |
| VerbKeyedDictionary   |  0.2783 ns | 0.0044 ns | 0.0037 ns |
