# NanoRoute ![Tests](https://sholtee.github.io/nanoroute/badges/tests-badge.svg) [![Coverage](https://sholtee.github.io/nanoroute/badges/coverage-badge.svg)](https://sholtee.github.io/nanoroute/CoverageReport/) ![GitHub License](https://img.shields.io/github/license/sholtee/nanoroute) [![NuGet Version](https://img.shields.io/nuget/v/nanoroute)](https://www.nuget.org/packages/nanoroute)

NanoRoute is a small, dependency-light routing library for `HttpRequestMessage` pipelines. It includes optional `HttpListener` and AWS Lambda adapters plus focused helpers for JSON payloads, query binding, and error handling.

## Directory Structure

```text
nanoroute/
|-- Src/                        Main source code
|   |-- NanoRoute/              Library project
|       |-- Doc/                DocFX documentation sources
|       |-- Public/             Public API surface
|       |-- Private/            Internal implementation details
|       |   |-- LowLevel/       Low-level path and buffer helpers
|       |   |-- RoutePattern/   Route pattern parsing and matching
|       |-- Properties/         Resources and generated metadata
|       |-- Icon.png            NuGet package icon
|       |-- NanoRoute.csproj    Project file
|       |-- PublicAPI.*.txt     Public API analyzer baselines
|       |-- README.md           NuGet package README
|   |-- NanoRoute.AwsLambda/    AWS Lambda adapter package
|       |-- Public/             Public adapter API surface
|       |-- Private/            API Gateway DTO mapping helpers
|       |-- Properties/         Resources and generated metadata
|       |-- NanoRoute.AwsLambda.csproj
|       |-- PublicAPI.*.txt
|       |-- README.md
|-- Tests/                      Validation and benchmarks
|   |-- NanoRoute.Tests/        Unit tests
|   |-- NanoRoute.Perf/         Performance benchmarks
|   |-- NanoRoute.NativeAot/    Native AOT smoke test
|   |-- CoverageSettings.xml    Coverage configuration
|   |-- Directory.Build.props   Shared test project settings
|-- Scripts/                    Build and test helpers
|-- Directory.Build.props       Shared MSBuild settings
|-- NanoRoute.slnx              Solution entry point
```

## Target Frameworks

- Library: `netstandard2.0` and `netstandard2.1`
- Native AOT validation: `Tests/NanoRoute.NativeAot`
