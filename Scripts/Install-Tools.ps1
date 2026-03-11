#
# Install-Tools.ps1
#
# Author: Denes Solti
#

$ErrorActionPreference = "Stop"

$ENV:DOTNET_CLI_TELEMETRY_OPTOUT = $True

$oldLocation = Get-Location
Set-Location (Join-Path $PSScriptRoot "..")
try {
  dotnet tool restore
  if (-not $?) { throw "Failed to install dotnet tools" }
} finally {
  Set-Location $oldLocation
}