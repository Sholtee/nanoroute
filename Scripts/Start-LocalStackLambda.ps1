#
# Start-LocalStackLambda.ps1
#
# Author: Denes Solti
#

param
(
  [string] $project = "NanoRoute.TestLambda",
  [string] $functionName = "nanoroute-test-lambda",
  [string] $handler = "NanoRoute.TestLambda::NanoRoute.TestLambda.LambdaFunction::Handler",
  [string] $runtime = "dotnet8",
  [string] $region = "us-east-1",
  [string] $containerName = "nanoroute-localstack",
  [string] $localStackImage = "localstack/localstack:2026.04.0",
  [int] $port = 4566,
  [int] $timeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

$root = Join-Path $PSScriptRoot ".." | Resolve-Path

function Invoke-Docker
{
  docker @args
  if (-not $?) { throw "Docker command failed: docker $($args -join ' ')" }
}

function Invoke-LocalAws
{
  Invoke-Docker exec `
    -e AWS_ACCESS_KEY_ID=test `
    -e AWS_SECRET_ACCESS_KEY=test `
    -e AWS_DEFAULT_REGION=$region `
    $containerName awslocal @args
}

function Wait-LocalStack
{
  $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds)

  do
  {
    try
    {
      $response = Invoke-RestMethod -Uri "http://localhost:$port/_localstack/health" -Method Get -TimeoutSec 2
      if ($response.services.lambda -in @("available", "running")) { return }
    }
    catch { Write-Host $_  }

    Start-Sleep -Seconds 1
  }
  while ([DateTime]::UtcNow -lt $deadline)

  throw "LocalStack did not become ready within $timeoutSeconds seconds."
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw "Docker CLI was not found." }

$existingContainer = Invoke-Docker ps -aq --filter "name=^/$containerName$"
if ($existingContainer) {
  Write-Host "Remove existing container"
  Invoke-Docker rm -f $containerName | Out-Null
}

Invoke-Docker run `
  --detach `
  --name $containerName `
  --publish "${port}:4566" `
  --env "SERVICES=lambda,iam,logs" `
  --env "AWS_DEFAULT_REGION=$region" `
  --volume "/var/run/docker.sock:/var/run/docker.sock" `
  $localStackImage | Out-Null

Wait-LocalStack

$packageOutput = & (Join-Path $PSScriptRoot "Package-Lambda.ps1") $project
if (-not $?)
{
  $packageOutput | Write-Error
  throw "Failed to package Lambda project '$project'."
}

$packagePath =  [System.IO.Path]::Combine($root, "BIN", "Publish", "$project.zip")
if (-not (Test-Path $packagePath)) { throw "Lambda package was not created: $packagePath" }

$containerPackagePath = "/tmp/$project.zip"

Invoke-Docker cp $packagePath "${containerName}:$containerPackagePath" | Out-Null

Invoke-LocalAws lambda create-function `
  --function-name $functionName `
  --runtime $runtime `
  --role "arn:aws:iam::000000000000:role/lambda-role" `
  --handler $handler `
  --zip-file "fileb://$containerPackagePath" `
  --timeout 30 `
  --memory-size 256 | Out-Null

Invoke-LocalAws lambda wait function-active-v2 --function-name $functionName

$endpoint = Invoke-LocalAws lambda create-function-url-config `
  --function-name $functionName `
  --auth-type NONE `
  --query FunctionUrl `
  --output text
if ([string]::IsNullOrWhiteSpace($endpoint)) { throw "LocalStack did not return a Lambda function URL." }

$endpoint
