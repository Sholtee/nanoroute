# NanoRoute ![Tests](https://sholtee.github.io/nanoroute/badges/tests-badge.svg) [![Coverage](https://sholtee.github.io/nanoroute/badges/coverage-badge.svg)](https://sholtee.github.io/nanoroute/CoverageReport/) ![GitHub License](https://img.shields.io/github/license/sholtee/nanoroute) [![NuGet Version](https://img.shields.io/nuget/v/nanoroute)](https://www.nuget.org/packages/nanoroute)

NanoRoute is a small, dependency-light routing library for `HttpRequestMessage` pipelines. It includes optional `HttpListener` and AWS Lambda adapters plus focused helpers for JSON payloads, query binding, endpoint-local middleware, and error handling. Custom transports can compose `RequestPipeline` directly.

## Install

```shell
dotnet add package NanoRoute --prerelease
```

For AWS Lambda HTTP API and Lambda Function URL support, also install the adapter package:

```shell
dotnet add package NanoRoute.AwsLambda --prerelease
```

## First Route

```csharp
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using NanoRoute;

HttpListenerRouter router = HttpListenerRouter
    .CreateBuilder()
    .AddEndpoint("GET", "/health/", endpoint => endpoint
        .WithHandler(static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ok")
        })))
    .CreateRouter();
```

From there, add `AddDefaultValueParsers()` for route parameters, `WithQueryBindings()` for query strings, `WithJsonBody()` for request bodies, and typed handlers when you want route values, services, and framework values projected into a request object.

## Directory Structure

```text
nanoroute/
|-- .github/
|   |-- workflows/                      GitHub Actions workflows
|-- Scripts/                            Build, packaging, docs, benchmark chart, and local helper scripts
|-- Src/                                Main source code
|   |-- Directory.Build.props           Shared source project settings
|   |-- Directory.Build.targets         Shared source project targets
|   |-- NanoRoute/                      Library project
|   |   |-- Doc/                        DocFX documentation sources
|   |   |   |-- docfx.json              DocFX build configuration
|   |   |   |-- docfxFilters.yml        API filter rules
|   |   |   |-- index.md                Core package documentation landing page
|   |   |-- Private/                    Internal implementation details
|   |   |   |-- Dsl/                    Route DSL parsing helpers
|   |   |   |-- LowLevel/               Low-level path and buffer helpers
|   |   |-- Properties/                 Resources and generated metadata
|   |   |-- Public/                     Public API surface
|   |   |   |-- Extensions/             Public extension methods
|   |   |   |-- HttpListener/           HttpListener adapter API
|   |   |-- HISTORY.md                  Version history
|   |   |-- Icon.png                    NuGet package icon
|   |   |-- NanoRoute.csproj
|   |   |-- PublicAPI.*.txt             Public API analyzer baselines
|   |   |-- README.md                   NuGet package README
|   |-- NanoRoute.AwsLambda/            AWS Lambda adapter package
|       |-- Doc/                        DocFX documentation sources
|       |   |-- docfx.json              DocFX build configuration
|       |   |-- index.md                AWS Lambda package documentation landing page
|       |-- Private/                    API Gateway DTO mapping helpers
|       |-- Properties/                 Resources and generated metadata
|       |-- Public/                     Public adapter API surface
|       |-- HISTORY.md                  Version history
|       |-- Icon.png                    NuGet package icon
|       |-- NanoRoute.AwsLambda.csproj
|       |-- PublicAPI.*.txt             Public API analyzer baselines
|       |-- README.md                   NuGet package README
|-- Tests/                              Validation, integration, smoke, and benchmark projects
|   |-- NanoRoute.AwsLambda.Tests/      AWS Lambda adapter tests
|   |-- NanoRoute.NativeAot/            Native AOT smoke test
|   |-- NanoRoute.Perf/                 Performance benchmarks
|   |-- NanoRoute.TestLambda/           LocalStack Lambda test project
|   |-- NanoRoute.Tests/                Core unit tests
|   |-- CoverageSettings.xml            Coverage configuration
|   |-- Directory.Build.props           Shared test project settings
|   |-- UnitTests.props                 Shared unit test project settings
|-- .editorconfig
|-- .gitignore
|-- AGENTS.md                           Repository-specific agent instructions
|-- Directory.Build.props               Shared MSBuild settings
|-- dotnet-tools.json                   Local .NET tool manifest
|-- LICENSE
|-- NanoRoute.slnx                      Solution entry point
|-- README.md                           Project README
```

## Route matching performance

![RouteMatchingPerformance](https://github.com/Sholtee/nanoroute/blob/gh-pages/Benchmarks/NanoRoute.Perf.MatcherBenchmarks-barplot.png)

## Target Frameworks

- Core library: `netstandard2.0` and `netstandard2.1`
- AWS Lambda adapter: `net8.0`
- Native AOT validation: `Tests/NanoRoute.NativeAot`
