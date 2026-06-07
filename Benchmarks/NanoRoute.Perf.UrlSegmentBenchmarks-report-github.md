```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method           | Scenario | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------- |--------- |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| **UriSegments**      | **Short**    | **115.19 ns** | **2.305 ns** | **2.655 ns** |  **1.00** |    **0.03** | **0.0157** |     **264 B** |        **1.00** |
| DelimitedSegment | Short    |  32.23 ns | 0.031 ns | 0.029 ns |  0.28 |    0.01 |      - |         - |        0.00 |
|                  |          |           |          |          |       |         |        |           |             |
| **UriSegments**      | **Long**     | **271.88 ns** | **4.232 ns** | **3.751 ns** |  **1.00** |    **0.02** | **0.0439** |     **736 B** |        **1.00** |
| DelimitedSegment | Long     | 122.34 ns | 0.092 ns | 0.072 ns |  0.45 |    0.01 |      - |         - |        0.00 |
|                  |          |           |          |          |       |         |        |           |             |
| **UriSegments**      | **Escaped**  | **137.01 ns** | **1.804 ns** | **1.688 ns** |  **1.00** |    **0.02** | **0.0219** |     **368 B** |        **1.00** |
| DelimitedSegment | Escaped  |  39.06 ns | 0.036 ns | 0.033 ns |  0.29 |    0.00 |      - |         - |        0.00 |
