```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4


```
| Method            | Scenario        | Mean        | Error     | StdDev    | Gen0   | Allocated |
|------------------ |---------------- |------------:|----------:|----------:|-------:|----------:|
| **ParseRoutePattern** | **Empty**           |    **14.49 ns** |  **0.217 ns** |  **0.181 ns** | **0.0022** |      **56 B** |
| **ParseRoutePattern** | **LiteralSegments** |   **602.28 ns** |  **4.209 ns** |  **3.515 ns** | **0.0572** |    **1440 B** |
| **ParseRoutePattern** | **SingleParameter** |   **781.66 ns** |  **6.382 ns** |  **5.658 ns** | **0.0648** |    **1640 B** |
| **ParseRoutePattern** | **MixedParameters** | **2,428.60 ns** | **19.836 ns** | **18.555 ns** | **0.1907** |    **4840 B** |
| **ParseRoutePattern** | **ParserArguments** | **2,336.91 ns** | **15.427 ns** | **13.675 ns** | **0.1755** |    **4408 B** |
| **ParseRoutePattern** | **LongRoute**       | **5,033.82 ns** | **21.838 ns** | **20.427 ns** | **0.3967** |   **10144 B** |
