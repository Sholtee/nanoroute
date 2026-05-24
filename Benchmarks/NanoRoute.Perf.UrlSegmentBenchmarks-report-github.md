```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v4


```
| Method           | Scenario | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|----------------- |--------- |----------:|---------:|---------:|------:|-------:|----------:|------------:|
| **UriSegments**      | **Short**    | **126.49 ns** | **0.785 ns** | **0.613 ns** |  **1.00** | **0.0105** |     **264 B** |        **1.00** |
| DelimitedSegment | Short    |  34.74 ns | 0.044 ns | 0.041 ns |  0.27 |      - |         - |        0.00 |
|                  |          |           |          |          |       |        |           |             |
| **UriSegments**      | **Long**     | **322.03 ns** | **3.648 ns** | **3.412 ns** |  **1.00** | **0.0291** |     **736 B** |        **1.00** |
| DelimitedSegment | Long     | 153.57 ns | 0.226 ns | 0.189 ns |  0.48 |      - |         - |        0.00 |
|                  |          |           |          |          |       |        |           |             |
| **UriSegments**      | **Escaped**  | **155.02 ns** | **0.776 ns** | **0.606 ns** |  **1.00** | **0.0145** |     **368 B** |        **1.00** |
| DelimitedSegment | Escaped  |  50.61 ns | 1.028 ns | 1.009 ns |  0.33 |      - |         - |        0.00 |
