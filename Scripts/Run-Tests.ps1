#
# Run-Tests.ps1
#
# Author: Denes Solti
#

$ErrorActionPreference = "Stop"

$ENV:DOTNET_CLI_TELEMETRY_OPTOUT = $True

& (Join-Path $PSScriptRoot "Install-Tools.ps1")

$ROOT = Join-Path $PSScriptRoot ".." | Resolve-Path
$ARTIFACTS = Join-Path $ROOT "Artifacts"

foreach ($dir in @("Artifacts"; "BIN"; "OBJ")) {
  Remove-Item (Join-Path $ROOT $dir)  -Recurse -Force -ErrorAction SilentlyContinue
}

function Run-Tests([Parameter(Position=0, Mandatory=$true)][string] $csproj, [Parameter(Position=1, Mandatory=$true)][string] $display) {
  Write-Host "`n-------------------------------------------$display-------------------------------------------"
  dotnet build-server shutdown

  $shortName = [System.IO.Path]::GetFileNameWithoutExtension($csproj)
  $csproj = (Get-ChildItem -Path (Join-Path $ROOT "Tests") -Recurse -Filter $csproj).FullName

  dotnet tool run dotnet-coverage collect `
    --settings ([System.IO.Path]::Combine($ROOT, "Tests", "CoverageSettings.xml")) `
    --output (Join-Path $ARTIFACTS "$shortName.Coverage.xml") `
    "dotnet test $csproj --configuration:Debug --logger:`"junit;LogFilePath=$(Join-Path $ARTIFACTS "$shortName.Results.xml")`""

  if (-not $?) { throw "Test session failed" }
}

Run-Tests 'NanoRoute.Tests.csproj' 'Regular tests (with coverage)'
Run-Tests 'NanoRoute.AwsLambda.Tests.csproj' 'AWS Lambda tests (with coverage)'

dotnet tool run reportgenerator `
  -reports:((Get-ChildItem -Path $ARTIFACTS -File -Filter '*.Coverage.xml' | Select-Object -ExpandProperty FullName) -join ';') `
  -targetdir:(Join-Path $ARTIFACTS 'CoverageReport') `
  -reporttypes:Html_Dark
