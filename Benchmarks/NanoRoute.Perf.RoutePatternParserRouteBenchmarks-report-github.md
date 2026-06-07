```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method            | Scenario        | Mean        | Error     | StdDev    | Gen0   | Allocated |
|------------------ |---------------- |------------:|----------:|----------:|-------:|----------:|
| **ParseRoutePattern** | **Empty**           |    **14.05 ns** |  **0.139 ns** |  **0.130 ns** | **0.0033** |      **56 B** |
| **ParseRoutePattern** | **LiteralSegments** |   **526.57 ns** |  **4.168 ns** |  **3.899 ns** | **0.0858** |    **1440 B** |
| **ParseRoutePattern** | **SingleParameter** |   **689.35 ns** | **12.939 ns** | **12.104 ns** | **0.0973** |    **1640 B** |
| **ParseRoutePattern** | **MixedParameters** | **2,124.36 ns** | **30.574 ns** | **28.599 ns** | **0.2861** |    **4840 B** |
| **ParseRoutePattern** | **ParserArguments** | **2,093.62 ns** | **22.431 ns** | **20.982 ns** | **0.2632** |    **4408 B** |
| **ParseRoutePattern** | **LongRoute**       | **4,327.80 ns** | **46.798 ns** | **39.078 ns** | **0.6027** |   **10144 B** |
