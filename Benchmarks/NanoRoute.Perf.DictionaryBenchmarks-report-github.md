```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method                | Mean       | Error     | StdDev    |
|---------------------- |-----------:|----------:|----------:|
| StringKeyedDictionary | 16.4537 ns | 0.0197 ns | 0.0175 ns |
| VerbKeyedDictionary   |  0.2789 ns | 0.0028 ns | 0.0025 ns |
