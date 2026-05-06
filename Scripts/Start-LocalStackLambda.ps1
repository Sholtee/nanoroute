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
  [string] $localStackImage = "localstack/localstack:4.14.0",
  [string] $lambdaUrlEnvVar = "NANOROUTE_TEST_LAMBDA_URL",
  [int] $port = 4566,
  [int] $timeoutSeconds = 30
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

function Wait-LocalStack
{
  $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds)
  do
  {
    try
    {
      $state = (Invoke-RestMethod -Uri "http://localhost:$port/_localstack/health" -Method Get -TimeoutSec 2 -ErrorAction Stop).services.lambda
      if ($state -in @("available", "running")) { return }
    }
    catch { }

    Start-Sleep -Seconds 1
  }
  while ([DateTime]::UtcNow -lt $deadline)

  Invoke-Docker logs --tail 120 $containerName

  throw "LocalStack did not become ready within $timeoutSeconds seconds."
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw "Docker CLI was not found." }

if ((Invoke-Docker ps -aq --filter "name=^/$containerName$")) {
  Write-Host -NoNewLine "Remove existing container......"
  Invoke-Docker rm -f $containerName | Out-Null
  Write-Host "OK" -ForegroundColor Green
}

Write-Host -NoNewLine "Starting the LocalStack container......"
Invoke-Docker run `
  --detach `
  --name $containerName `
  --publish "${port}:4566" `
  --env "SERVICES=lambda,iam,logs" `
  --env "AWS_DEFAULT_REGION=$region" `
  --volume "/var/run/docker.sock:/var/run/docker.sock" `
  $localStackImage | Out-Null

Wait-LocalStack
Write-Host "OK" -ForegroundColor Green

Write-Host -NoNewLine "Creating the deploy package......"
& (Join-Path $PSScriptRoot "Package-Lambda.ps1") $project | Out-Null

$packagePath =  [System.IO.Path]::Combine($PSScriptRoot, "..", "BIN", "Publish", "$project.zip")
if (-not (Test-Path $packagePath)) { throw "Lambda package was not created: $packagePath" }
Write-Host "OK" -ForegroundColor Green

Write-Host -NoNewLine "Deploying the package......"
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
$endpoint = $endpoint.Trim()
if ([string]::IsNullOrWhiteSpace($endpoint)) { throw "LocalStack did not return a Lambda function URL." }
Write-Host "OK" -ForegroundColor Green

Write-Host -NoNewLine "Checking the health endpoint......"
$health = Invoke-RestMethod -Uri "$($endpoint.TrimEnd('/'))/health" -Method Get -TimeoutSec 10 -ErrorAction Stop
if ($health -ne "ok") { throw "Health endpoint returned unexpected response: $health" }
Write-Host "OK" -ForegroundColor Green

Set-Item -Path "ENV:$lambdaUrlEnvVar" -Value $endpoint

if (-not [string]::IsNullOrWhiteSpace($ENV:GITHUB_ENV)) { Add-Content -Path $ENV:GITHUB_ENV -Value "$lambdaUrlEnvVar=$endpoint" }

Write-Host "$lambdaUrlEnvVar=$endpoint"