#
# Build-Docs.ps1
#
# Author: Denes Solti
#

$ErrorActionPreference = "Stop"

$ENV:DOTNET_CLI_TELEMETRY_OPTOUT = $True

& (Join-Path $PSScriptRoot "Install-Tools.ps1")

$ROOT = Join-Path $PSScriptRoot ".." | Resolve-Path

# used in docfx.json
$ENV:PUBLISH_DIR = [System.IO.Path]::Combine($ROOT, "BIN", "Published")

Remove-Item ([System.IO.Path]::Combine($ROOT, "Artifacts", "docs")) -Recurse -Force -ErrorAction SilentlyContinue

foreach ($csproj in (Get-ChildItem -Path $ROOT -Filter *.csproj -Recurse)) {
  $docfxJson = [System.IO.Path]::Combine($csproj.Directory.FullName, "Doc", "docfx.json")
  if (-not (Test-Path $docfxJson)) { continue }

  Write-Host "`n---------Generate documentation for $($csproj.Name)---------"

  foreach ($dir in "BIN", "OBJ") {
    Remove-Item (Join-Path $ROOT $dir) -Recurse -Force -ErrorAction SilentlyContinue
  }

  $tfms = (dotnet msbuild $csproj -nologo -getProperty:TargetFrameworks).Trim()
  if ([string]::IsNullOrWhiteSpace($tfms)) { $tfms = (dotnet msbuild $csproj -nologo -getProperty:TargetFramework).Trim() }

  $tfm = $tfms -split ';' | Where-Object { $_ } | Select-Object -Last 1
  Write-Host "TFM: $tfm"

  dotnet publish $csproj.FullName -c Release -f $tfm -o $env:PUBLISH_DIR
  if (-not $?) { throw "Project compilation failed" }

  dotnet tool run docfx $docfxJson
  if (-not $?) { throw "Failed to compile the docs" }

  Write-Host "-------------------------------Done-------------------------------`n"
}