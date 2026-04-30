```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method | Scenario                    | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------- |---------------------------- |-----------:|---------:|---------:|-------:|----------:|
| **Parse**  | **AllParametersProvided**       |   **284.3 ns** |  **0.54 ns** |  **0.45 ns** | **0.0329** |     **552 B** |
| **Parse**  | **OptionalParameterMissing**    |   **149.6 ns** |  **0.98 ns** |  **0.87 ns** | **0.0296** |     **496 B** |
| **Parse**  | **UndeclaredParametersPresent** |   **246.0 ns** |  **0.85 ns** |  **0.75 ns** | **0.0296** |     **496 B** |
| **Parse**  | **RequiredParameterMissing**    | **2,465.7 ns** | **15.37 ns** | **12.84 ns** | **0.0687** |    **1184 B** |
