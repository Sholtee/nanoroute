```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method           | Scenario | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|----------------- |--------- |----------:|---------:|---------:|------:|-------:|----------:|------------:|
| **UriSegments**      | **Short**    | **118.52 ns** | **0.911 ns** | **0.852 ns** |  **1.00** | **0.0157** |     **264 B** |        **1.00** |
| DelimitedSegment | Short    |  30.39 ns | 0.629 ns | 0.699 ns |  0.26 |      - |         - |        0.00 |
|                  |          |           |          |          |       |        |           |             |
| **UriSegments**      | **Long**     | **276.34 ns** | **2.109 ns** | **1.972 ns** |  **1.00** | **0.0439** |     **736 B** |        **1.00** |
| DelimitedSegment | Long     | 119.60 ns | 0.084 ns | 0.078 ns |  0.43 |      - |         - |        0.00 |
|                  |          |           |          |          |       |        |           |             |
| **UriSegments**      | **Escaped**  | **137.13 ns** | **0.909 ns** | **0.850 ns** |  **1.00** | **0.0219** |     **368 B** |        **1.00** |
| DelimitedSegment | Escaped  |  39.21 ns | 0.063 ns | 0.059 ns |  0.29 |      - |         - |        0.00 |
