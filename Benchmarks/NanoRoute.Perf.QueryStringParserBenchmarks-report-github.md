```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32522/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method | Scenario                    | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------- |---------------------------- |-----------:|---------:|---------:|-------:|----------:|
| **Parse**  | **AllParametersProvided**       |   **423.2 ns** |  **5.45 ns** |  **4.83 ns** | **0.0305** |     **512 B** |
| **Parse**  | **OptionalParameterMissing**    |   **194.4 ns** |  **0.68 ns** |  **0.56 ns** | **0.0248** |     **416 B** |
| **Parse**  | **UndeclaredParametersPresent** |   **358.1 ns** |  **2.29 ns** |  **2.03 ns** | **0.0281** |     **472 B** |
| **Parse**  | **RequiredParameterMissing**    | **6,027.9 ns** | **21.52 ns** | **17.97 ns** | **0.1144** |    **2016 B** |
