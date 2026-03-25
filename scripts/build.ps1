#!/usr/bin/env pwsh
# Cross-platform build script for GV Research Platform
# Works on Windows 11 and Ubuntu 22.04+

param(
    [ValidateSet('clean', 'restore', 'build', 'test', 'publish', 'docker-build')]
    [string]$Target = 'build'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $RepoRoot 'GvResearch.sln'

function Invoke-Clean {
    Write-Host "Cleaning..." -ForegroundColor Cyan
    dotnet clean $Solution -v q
    Get-ChildItem $RepoRoot -Recurse -Directory -Include bin, obj | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

function Invoke-Restore {
    Write-Host "Restoring..." -ForegroundColor Cyan
    dotnet restore $Solution
}

function Invoke-Build {
    Invoke-Restore
    Write-Host "Building..." -ForegroundColor Cyan
    dotnet build $Solution --no-restore -c Release
}

function Invoke-Test {
    Invoke-Build
    Write-Host "Testing..." -ForegroundColor Cyan
    dotnet test $Solution --no-build -c Release `
        --collect:"XPlat Code Coverage" `
        --results-directory (Join-Path $RepoRoot 'TestResults') `
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
}

function Invoke-Publish {
    Invoke-Build
    Write-Host "Publishing..." -ForegroundColor Cyan
    $projects = @(
        'src/GvResearch.Api/GvResearch.Api.csproj',
        'src/GvResearch.Sip/GvResearch.Sip.csproj',
        'src/GvResearch.Softphone/GvResearch.Softphone.csproj',
        'src/Iaet.Cli/Iaet.Cli.csproj'
    )
    foreach ($proj in $projects) {
        $fullPath = Join-Path $RepoRoot $proj
        if (Test-Path $fullPath) {
            dotnet publish $fullPath --no-build -c Release -o (Join-Path $RepoRoot "artifacts/$((Get-Item $fullPath).BaseName)")
        }
    }
}

function Invoke-DockerBuild {
    Write-Host "Building Docker images..." -ForegroundColor Cyan
    docker compose -f (Join-Path $RepoRoot 'scripts/docker-compose.yml') build
}

switch ($Target) {
    'clean'        { Invoke-Clean }
    'restore'      { Invoke-Restore }
    'build'        { Invoke-Build }
    'test'         { Invoke-Test }
    'publish'      { Invoke-Publish }
    'docker-build' { Invoke-DockerBuild }
}

Write-Host "Done: $Target" -ForegroundColor Green
