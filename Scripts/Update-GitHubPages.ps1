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

$ROOT = Join-Path $PSScriptRoot ".." | Resolve-Path
$GH_PAGES = Join-Path $ROOT "gh-pages"

if ([string]::IsNullOrWhiteSpace($githubToken)) { throw "GitHub token is not available" }

Remove-Item $GH_PAGES -Recurse -Force -ErrorAction SilentlyContinue

git config --global user.name "github-actions[bot]"
if (!$?) { throw "Failed to configure git user name" }

git config --global user.email "github-actions[bot]@users.noreply.github.com"
if (!$?) { throw "Failed to configure git user email" }

git clone --branch gh-pages "https://x-access-token:$githubToken@github.com/sholtee/nanoroute.git" $GH_PAGES
if (!$?) { throw "Failed to clone gh-pages" }

& $updateContent $ROOT $GH_PAGES

Set-Location $GH_PAGES

git add .
if (!$?) { throw "Failed to stage artifacts" }

# "git diff --quiet" returns 1 when differences are found.
git diff --cached --quiet
if ($LASTEXITCODE -eq 0) {
  Write-Host "Nothing to commit"
  return
}
if ($LASTEXITCODE -ne 1) { throw "Failed to inspect staged changes" }

git commit -m "[bot] update artifacts"
if (!$?) { throw "Failed to commit artifacts" }

git push origin gh-pages
if (!$?) { throw "Failed to push artifacts" }
