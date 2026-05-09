#
# Update-GitHubPages.ps1
#
# Author: Denes Solti
#

param(
  [Parameter(Position = 0, Mandatory = $true)]
  [scriptblock] $updateContent,
  [Parameter(Position = 1, Mandatory = $true)]
  [string] $githubToken
)

$ErrorActionPreference = "Stop"

function Invoke-Git
{
  $output = git @args
  if (-not $?) { throw "Git command failed" }

  return $output
}

$ROOT = Join-Path $PSScriptRoot ".." | Resolve-Path
$GH_PAGES = Join-Path $ROOT "gh-pages"

if ([string]::IsNullOrWhiteSpace($githubToken)) { throw "GitHub token is not available" }

Remove-Item $GH_PAGES -Recurse -Force -ErrorAction SilentlyContinue

Invoke-Git config --global user.name "github-actions[bot]"
Invoke-Git config --global user.email "github-actions[bot]@users.noreply.github.com"
Invoke-Git clone --branch gh-pages "https://x-access-token:$githubToken@github.com/sholtee/nanoroute.git" $GH_PAGES

& $updateContent $ROOT $GH_PAGES

Set-Location $GH_PAGES

Invoke-Git add .

# "git diff --quiet" returns 1 when differences are found.
git diff --cached --quiet
if ($LASTEXITCODE -eq 0) {
  Write-Host "Nothing to commit"
  return
}
if ($LASTEXITCODE -ne 1) { throw "Failed to inspect staged changes" }

Invoke-Git commit -m "[bot] update artifacts"
Invoke-Git push origin gh-pages
