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

function Run-Tests([Parameter(Position=0, Mandatory=$true)][System.IO.FileInfo] $project) {
  $shortName = [System.IO.Path]::GetFileNameWithoutExtension($project.Name)

  Write-Host "`n-------------------------------------------$shortName tests (with coverage)-------------------------------------------"
  dotnet build-server shutdown

  dotnet tool run dotnet-coverage collect `
    --settings ([System.IO.Path]::Combine($ROOT, "Tests", "CoverageSettings.xml")) `
    --output (Join-Path $ARTIFACTS "$shortName.Coverage.xml") `
    "dotnet test `"$($project.FullName)`" --configuration:Debug --logger:`"junit;LogFilePath=$(Join-Path $ARTIFACTS "$shortName.Results.xml")`""

  if (-not $?) { throw "Test session failed" }
}

$testProjects = @(
  Get-ChildItem -Path (Join-Path $ROOT "Tests") -Recurse -File -Filter "*.csproj" |
    Where-Object { $_.BaseName -match "\.Tests?$" } |
    Sort-Object -Property FullName
)
if ($testProjects.Count -eq 0) { throw "No test projects found" }

foreach ($testProject in $testProjects) {
  Run-Tests $testProject
}

dotnet tool run reportgenerator `
  -reports:((Get-ChildItem -Path $ARTIFACTS -File -Filter '*.Coverage.xml' | Select-Object -ExpandProperty FullName) -join ';') `
  -targetdir:(Join-Path $ARTIFACTS 'CoverageReport') `
  -reporttypes:Html_Dark
if (-not $?) { throw "Failed to generate the coverage report" }
