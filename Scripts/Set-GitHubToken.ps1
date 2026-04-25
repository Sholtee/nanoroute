#
# Set-GitHubToken.ps1
#
# Author: Denes Solti
#

param(
  [Parameter(Mandatory = $true)][securestring] $token
)

$ErrorActionPreference = "Stop"

if ($null -eq (Get-Module Microsoft.PowerShell.SecretManagement -ListAvailable)) {
  Install-Module Microsoft.PowerShell.SecretManagement -Scope CurrentUser -Force
}
Import-Module Microsoft.PowerShell.SecretManagement -ErrorAction Stop

$VAULT_NAME = "NanoRouteSecrets"
$TOKEN_NAME = "NanoRouteGitHubIssueToken"

Set-Secret -Vault $VAULT_NAME -Name $TOKEN_NAME -Secret $token

Write-Host "Secret registered: $VAULT_NAME/$TOKEN_NAME"
