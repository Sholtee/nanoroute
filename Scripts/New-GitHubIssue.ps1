#
# New-GitHubIssue.ps1
#
# Author: Denes Solti
#

param(
  [Parameter(Position = 0, Mandatory = $true)][string] $title,
  [Parameter(Position = 1, Mandatory = $true)][string] $bodyFile,
  [Parameter()][string[]] $labels = @(),
  [Parameter()][string[]] $assignees = @(),
  [Parameter()][switch] $dryRun
)

$ErrorActionPreference = "Stop"

if ($null -eq (Get-Module Microsoft.PowerShell.SecretManagement -ListAvailable)) {
  Install-Module Microsoft.PowerShell.SecretManagement -Scope CurrentUser -Force
}
Import-Module Microsoft.PowerShell.SecretManagement -ErrorAction Stop

$OWNER = "Sholtee"
$REPO = "nanoroute"
$VAULT_NAME = "NanoRouteSecrets"
$TOKEN_NAME = "NanoRouteGitHubIssueToken"

if (-not (Test-Path $bodyFile -PathType Leaf)) { throw "Body file not found: $bodyFile" }

$body = Get-Content -Path $bodyFile -Raw

$payload = [ordered] @{
  title = $title
  body = $body
}

if ($labels.Length -gt 0) { $payload.labels = $labels }

if ($assignees.Length -gt 0) { $payload.assignees = $assignees }

$json = $payload | ConvertTo-Json -Depth 10

if ($dryRun) {
  Write-Host $json
  return
}

$token = Get-Secret -Vault $VAULT_NAME -Name $TOKEN_NAME -AsPlainText
if ([string]::IsNullOrWhiteSpace($token)) { throw "Secret not found: $TOKEN_NAME" }

$response = Invoke-RestMethod `
  -Method Post `
  -Uri "https://api.github.com/repos/$OWNER/$REPO/issues" `
  -Headers @{
    Authorization = "Bearer $token"
    # Requests GitHub's REST API JSON response format.
    Accept = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
    # Identifies this repo's helper script as the API client.
    "User-Agent" = "nanoroute"
  } `
  -Body $json `
  -ContentType "application/json"

Write-Host $response.html_url
