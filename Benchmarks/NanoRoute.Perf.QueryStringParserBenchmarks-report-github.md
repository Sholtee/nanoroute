```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32690/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method | Scenario                    | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------- |---------------------------- |-----------:|---------:|---------:|-------:|----------:|
| **Parse**  | **AllParametersProvided**       |   **383.7 ns** |  **3.90 ns** |  **3.64 ns** | **0.0210** |     **536 B** |
| **Parse**  | **OptionalParameterMissing**    |   **212.3 ns** |  **2.15 ns** |  **1.90 ns** | **0.0191** |     **480 B** |
| **Parse**  | **UndeclaredParametersPresent** |   **323.8 ns** |  **3.16 ns** |  **2.80 ns** | **0.0191** |     **480 B** |
| **Parse**  | **RequiredParameterMissing**    | **3,065.0 ns** | **20.48 ns** | **19.16 ns** | **0.0458** |    **1168 B** |
