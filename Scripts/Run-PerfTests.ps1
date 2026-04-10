#
# Run-PerfTests.ps1
#
# Author: Denes Solti
#

param([Parameter(Position = 0)][string]$filter = "*")

$ErrorActionPreference = "Stop"

$ENV:DOTNET_CLI_TELEMETRY_OPTOUT = $True

$ROOT = Join-Path $PSScriptRoot ".." | Resolve-Path
$PROJECT = (Get-ChildItem -Path $ROOT -Recurse -Filter "NanoRoute.Perf.csproj").FullName

Remove-Item (Join-Path $ROOT "Artifacts") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $ROOT "BIN") -Recurse -Force -ErrorAction SilentlyContinue

dotnet build-server shutdown

dotnet build $PROJECT --configuration Release
if (-not $?) { throw "Build failed" }

& (Get-ChildItem -Path ([System.IO.Path]::Combine($ROOT, "BIN", "Release")) -Recurse -Filter "NanoRoute.Perf.exe") --filter $filter --artifacts ([System.IO.Path]::Combine($ROOT, "Artifacts", "BenchmarkDotNet"))
if (-not $?) { throw "Test session failed" }