```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.32860/24H2/2024Update/HudsonValley) (Hyper-V)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method                                            | branchLookupKind | Mean     | Error   | StdDev  | Allocated |
|-------------------------------------------------- |----------------- |---------:|--------:|--------:|----------:|
| **WalkGraphUsingRouteMatchCursor**                    | **?**                | **205.2 ns** | **0.20 ns** | **0.17 ns** |         **-** |
| **MatchLiteralSegmentsAndWalkGraph**                  | **SingleBranch**     | **150.0 ns** | **0.19 ns** | **0.17 ns** |         **-** |
| SearchPercentThenMatchLiteralSegmentsAndWalkGraph | SingleBranch     | 177.4 ns | 0.40 ns | 0.35 ns |         - |
| **MatchLiteralSegmentsAndWalkGraph**                  | **LiteralBranches**  | **165.3 ns** | **0.34 ns** | **0.29 ns** |         **-** |
| SearchPercentThenMatchLiteralSegmentsAndWalkGraph | LiteralBranches  | 190.4 ns | 0.33 ns | 0.28 ns |         - |
