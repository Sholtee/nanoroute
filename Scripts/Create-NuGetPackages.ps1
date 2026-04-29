#
# Create-NuGetPackages.ps1
#
# Author: Denes Solti
#

$ErrorActionPreference = "Stop"

$ENV:DOTNET_CLI_TELEMETRY_OPTOUT = $True

$ROOT = Join-Path $PSScriptRoot ".." | Resolve-Path
$SRC = Join-Path $ROOT "Src"
$ARTIFACTS = Join-Path $ROOT "Artifacts"
$PACKAGES = Join-Path $ARTIFACTS "NuGet"

Remove-Item $PACKAGES -Recurse -Force -ErrorAction SilentlyContinue

dotnet build-server shutdown

foreach ($csproj in (Get-ChildItem -Path $SRC -Recurse -File -Filter *.csproj)) {
  Write-Host "`n---------Create NuGet package for $($csproj.Name)---------"

  dotnet pack $csproj.FullName --configuration Release --output $PACKAGES --include-symbols -p:SymbolPackageFormat=snupkg
  if (-not $?) { throw "Package creation failed" }

  Write-Host "-------------------------------Done-------------------------------`n"
}
