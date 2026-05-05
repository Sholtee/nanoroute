#
# Dump-LocalStackLambdaLogs.ps1
#
# Author: Denes Solti
#

param
(
  [string] $functionName = "nanoroute-test-lambda",
  [string] $containerName = "nanoroute-localstack",
  [string] $region = "us-east-1",
  [string] $outputPath = ""
)

$ErrorActionPreference = "Stop"

function Invoke-Docker
{
  $output = docker @args
  if (-not $?) { throw "Docker command failed" }

  return $output
}

function Invoke-LocalAws
{
  Invoke-Docker exec `
    -e AWS_ACCESS_KEY_ID=test `
    -e AWS_SECRET_ACCESS_KEY=test `
    -e AWS_DEFAULT_REGION=$region `
    $containerName awslocal @args
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw "Docker CLI was not found." }
if (-not (Invoke-Docker ps -q --filter "name=^/$containerName$")) { throw "LocalStack container is not running: $containerName" }

$root = Join-Path $PSScriptRoot ".." | Resolve-Path
$logDir = [System.IO.Path]::Combine($root, "Artifacts", "Logs")
$logGroupName = "/aws/lambda/$functionName"

if ([string]::IsNullOrWhiteSpace($outputPath))
{
  $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
  $outputPath = Join-Path $logDir "$functionName-$timestamp.log"
}

$outputDir = Split-Path -Parent $outputPath
if (-not [string]::IsNullOrWhiteSpace($outputDir)) { New-Item -ItemType Directory -Path $outputDir -Force | Out-Null }

Set-Content -Path $outputPath -Value @(
  "Log group: $logGroupName",
  "Container: $containerName",
  "Region: $region",
  "Created: $(Get-Date -Format o)",
  ""
)

$streams = (Invoke-LocalAws logs describe-log-streams `
  --log-group-name $logGroupName `
  --order-by LastEventTime `
  --descending `
  --output json | ConvertFrom-Json).logStreams

if (-not $streams)
{
  Add-Content -Path $outputPath -Value "No log streams found."
  $outputPath
  exit 0
}

foreach ($stream in $streams)
{
  Add-Content -Path $outputPath -Value @(
    "",
    "================================================================================",
    "Log stream: $($stream.logStreamName)",
    "================================================================================"
  )

  $nextToken = $null

  do
  {
    $previousToken = $nextToken

    $args = @(
      "logs",
      "get-log-events",
      "--log-group-name", $logGroupName,
      "--log-stream-name", $stream.logStreamName,
      "--start-from-head",
      "--output", "json"
    )

    if ($nextToken) { $args += @("--next-token", $nextToken) }

    $page = Invoke-LocalAws @args | ConvertFrom-Json

    foreach ($event in $page.events)
    {
      $eventTime = [DateTimeOffset]::FromUnixTimeMilliseconds($event.timestamp).ToLocalTime().ToString("o")
      Add-Content -Path $outputPath -Value "[$eventTime] $($event.message.TrimEnd())"
    }

    $nextToken = $page.nextForwardToken
  }
  while ($nextToken -and $nextToken -ne $previousToken)
}

$outputPath
