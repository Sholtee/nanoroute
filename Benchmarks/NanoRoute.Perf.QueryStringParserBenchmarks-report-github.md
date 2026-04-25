```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method | Scenario                    | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------- |---------------------------- |-----------:|---------:|---------:|-------:|----------:|
| **Parse**  | **AllParametersProvided**       |   **442.6 ns** |  **4.24 ns** |  **3.97 ns** | **0.0305** |     **512 B** |
| **Parse**  | **OptionalParameterMissing**    |   **198.0 ns** |  **3.70 ns** |  **3.46 ns** | **0.0248** |     **416 B** |
| **Parse**  | **UndeclaredParametersPresent** |   **369.4 ns** |  **5.76 ns** |  **5.39 ns** | **0.0281** |     **472 B** |
| **Parse**  | **RequiredParameterMissing**    | **6,141.3 ns** | **37.77 ns** | **33.49 ns** | **0.1144** |    **2016 B** |
