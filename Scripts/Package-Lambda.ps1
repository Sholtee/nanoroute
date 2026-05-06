#
# Package-Lambda.ps1
#
# Author: Denes Solti
#

param([Parameter(Position=0, Mandatory=$true)][string]$project)

$ErrorActionPreference = "Stop"

$root = Join-Path $PSScriptRoot ".." | Resolve-Path
$csproj = (Get-ChildItem -Path $root -Recurse -Filter "$project.csproj").FullName
if (-not $csproj) { throw "Project not found" }

$shortName = [System.IO.Path]::GetFileNameWithoutExtension($csproj)
$publishDir = [System.IO.Path]::Combine($root, "BIN", "Publish")
$binDir = Join-Path $publishDir $shortName

dotnet publish $csproj -c Release -o $binDir -r "linux-x64" -p:PublishReadyToRun=true
if (-not $?) { throw "Failed to build the binaries" }

try {
  Compress-Archive -Path (Join-Path $binDir '*') -DestinationPath (Join-Path $publishDir "$shortName.zip") -Force
} finally {
  Remove-Item $binDir -Recurse -Force
}