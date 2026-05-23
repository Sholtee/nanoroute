#
# Create-NuGetPackages.ps1
#
# Author: Denes Solti
#

param(
  [Parameter(Position = 0, Mandatory = $true)]
  [ValidateSet("NanoRoute", "NanoRoute.AwsLambda")]
  [string] $project
)

$ErrorActionPreference = "Stop"

$ENV:DOTNET_CLI_TELEMETRY_OPTOUT = $True

$ROOT = Join-Path $PSScriptRoot ".." | Resolve-Path
$SRC = Join-Path $ROOT "Src"
$ARTIFACTS = Join-Path $ROOT "Artifacts"
$PACKAGES = Join-Path $ARTIFACTS "NuGet"

Remove-Item $PACKAGES -Recurse -Force -ErrorAction SilentlyContinue

$csproj = @(Get-ChildItem -Path $SRC -Recurse -File -Filter *.csproj | Where-Object { $_.BaseName -eq $project })
if ($csproj.Length -ne 1) { throw "Expected exactly one project named '$project' but found $($csproj.Length)" }

dotnet build-server shutdown

Write-Host "`n---------Create NuGet package for $($csproj[0].Name)---------"

dotnet pack $csproj[0].FullName --configuration Release --output $PACKAGES --include-symbols -p:SymbolPackageFormat=snupkg
if (-not $?) { throw "Package creation failed" }

Write-Host "-------------------------------Done-------------------------------`n"
