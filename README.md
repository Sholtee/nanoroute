# NanoRoute ![Tests](https://sholtee.github.io/nanoroute/badges/tests-badge.svg) [![Coverage](https://sholtee.github.io/nanoroute/badges/coverage-badge.svg)](https://sholtee.github.io/nanoroute/CoverageReport/)

NanoRoute is a small, dependency-light routing library for `HttpRequestMessage` pipelines. It includes an optional `HttpListener` adapter plus focused helpers for JSON payloads, query binding, and error handling.

## Directory Structure

```text
nanoroute/
|-- Src/                        Main source code
|   `-- NanoRoute/              Library project
|       |-- Public/             Public API surface
|       |-- Private/            Internal implementation details
|       |-- Properties/         Resources and generated metadata
|       |-- Doc/                DocFX documentation sources
|       |-- NanoRoute.csproj    Project file
|       `-- README.md           Package README
|-- Tests/                      Validation and benchmarks
|   |-- NanoRoute.Tests/        Unit tests
|   |-- NanoRoute.Perf/         Performance benchmarks
|   `-- NanoRoute.NativeAot/    Native AOT smoke test
|-- Scripts/                    Build and test helpers
|-- Directory.Build.props       Shared MSBuild settings
`-- NanoRoute.slnx              Solution entry point
```

## Target Frameworks

- Library: `netstandard2.0`
- Native AOT validation: `Tests/NanoRoute.NativeAot`
