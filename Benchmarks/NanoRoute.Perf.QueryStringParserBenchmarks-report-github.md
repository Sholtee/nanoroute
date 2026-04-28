```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method | Scenario                    | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------- |---------------------------- |-----------:|---------:|---------:|-------:|----------:|
| **Parse**  | **AllParametersProvided**       |   **499.5 ns** |  **3.19 ns** |  **2.83 ns** | **0.0210** |     **536 B** |
| **Parse**  | **OptionalParameterMissing**    |   **281.1 ns** |  **1.94 ns** |  **1.62 ns** | **0.0191** |     **480 B** |
| **Parse**  | **UndeclaredParametersPresent** |   **427.9 ns** |  **3.18 ns** |  **2.66 ns** | **0.0191** |     **480 B** |
| **Parse**  | **RequiredParameterMissing**    | **3,182.8 ns** | **44.13 ns** | **39.12 ns** | **0.0458** |    **1168 B** |
