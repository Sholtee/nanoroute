```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method | Scenario                    | Mean       | Error     | StdDev      | Median     | Gen0   | Allocated |
|------- |---------------------------- |-----------:|----------:|------------:|-----------:|-------:|----------:|
| **Parse**  | **AllParametersProvided**       |   **387.8 ns** |   **4.96 ns** |     **4.39 ns** |   **387.2 ns** | **0.0329** |     **552 B** |
| **Parse**  | **OptionalParameterMissing**    |   **226.1 ns** |   **4.49 ns** |    **11.84 ns** |   **221.4 ns** | **0.0296** |     **496 B** |
| **Parse**  | **UndeclaredParametersPresent** |   **380.3 ns** |   **7.64 ns** |    **11.44 ns** |   **376.9 ns** | **0.0296** |     **496 B** |
| **Parse**  | **RequiredParameterMissing**    | **4,556.4 ns** | **361.81 ns** | **1,066.81 ns** | **4,023.9 ns** | **0.0687** |    **1184 B** |
