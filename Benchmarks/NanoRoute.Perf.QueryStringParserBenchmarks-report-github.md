```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method | Scenario                    | Mean     | Error     | StdDev    | Gen0   | Allocated |
|------- |---------------------------- |---------:|----------:|----------:|-------:|----------:|
| **Parse**  | **AllParametersProvided**       |       **NA** |        **NA** |        **NA** |     **NA** |        **NA** |
| **Parse**  | **OptionalParameterMissing**    |       **NA** |        **NA** |        **NA** |     **NA** |        **NA** |
| **Parse**  | **UndeclaredParametersPresent** |       **NA** |        **NA** |        **NA** |     **NA** |        **NA** |
| **Parse**  | **RequiredParameterMissing**    | **3.636 μs** | **0.0080 μs** | **0.0071 μs** | **0.0648** |   **1.12 KB** |

Benchmarks with issues:
  QueryStringParserBenchmarks.Parse: DefaultJob [Scenario=AllParametersProvided]
  QueryStringParserBenchmarks.Parse: DefaultJob [Scenario=OptionalParameterMissing]
  QueryStringParserBenchmarks.Parse: DefaultJob [Scenario=UndeclaredParametersPresent]
