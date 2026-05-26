```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method            | Scenario        | Mean        | Error     | StdDev    | Gen0   | Allocated |
|------------------ |---------------- |------------:|----------:|----------:|-------:|----------:|
| **ParseRoutePattern** | **Empty**           |    **13.37 ns** |  **0.299 ns** |  **0.562 ns** | **0.0033** |      **56 B** |
| **ParseRoutePattern** | **LiteralSegments** |   **524.24 ns** | **10.017 ns** |  **9.370 ns** | **0.0858** |    **1440 B** |
| **ParseRoutePattern** | **SingleParameter** |   **734.08 ns** |  **5.237 ns** |  **4.899 ns** | **0.0973** |    **1640 B** |
| **ParseRoutePattern** | **MixedParameters** | **2,176.77 ns** | **10.888 ns** | **10.185 ns** | **0.2861** |    **4840 B** |
| **ParseRoutePattern** | **ParserArguments** | **2,219.77 ns** | **10.613 ns** |  **9.409 ns** | **0.2632** |    **4408 B** |
| **ParseRoutePattern** | **LongRoute**       | **4,593.18 ns** | **88.233 ns** | **82.533 ns** | **0.6027** |   **10144 B** |
