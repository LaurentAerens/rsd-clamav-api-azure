#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run BlackBox integration tests with Docker containers
.DESCRIPTION
    This script starts the required Docker containers (ClamAV daemon and API),
    waits for them to be healthy, runs the BlackBox tests, and cleans up.
.PARAMETER SkipBuild
    Skip rebuilding the Docker image (use existing image)
.PARAMETER KeepContainers
    Keep containers running after tests complete (useful for debugging)
.EXAMPLE
    .\scripts\run-blackbox-tests.ps1
.EXAMPLE
    .\scripts\run-blackbox-tests.ps1 -SkipBuild -KeepContainers
#>

param(
    [switch]$SkipBuild,
    [switch]$KeepContainers
)

$ErrorActionPreference = "Stop"

Write-Host "=================================" -ForegroundColor Cyan
Write-Host "BlackBox Test Runner" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Check if Docker is running
try {
    docker ps | Out-Null
} catch {
    Write-Host "❌ Docker is not running. Please start Docker Desktop and try again." -ForegroundColor Red
    exit 1
}

Write-Host "✓ Docker is running" -ForegroundColor Green

# Navigate to repository root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
Push-Location $repoRoot

try {
    # Stop and remove existing containers
    Write-Host ""
    Write-Host "Cleaning up existing containers..." -ForegroundColor Yellow
    docker-compose down 2>&1 | Out-Null
    
    # Build containers if requested
    if (-not $SkipBuild) {
        Write-Host ""
        Write-Host "Building Docker images (this may take a few minutes)..." -ForegroundColor Yellow
        docker-compose build
        if ($LASTEXITCODE -ne 0) {
            throw "Docker build failed"
        }
        Write-Host "✓ Docker images built successfully" -ForegroundColor Green
    }
    
    # Start containers
    Write-Host ""
    Write-Host "Starting containers..." -ForegroundColor Yellow
    docker-compose up -d
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start containers"
    }
    Write-Host "✓ Containers started" -ForegroundColor Green
    
    # Wait for API to be healthy
    Write-Host ""
    Write-Host "Waiting for API to be healthy..." -ForegroundColor Yellow
    $maxAttempts = 60
    $attempt = 0
    $healthy = $false
    
    while ($attempt -lt $maxAttempts) {
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:8080/healthz" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                $healthy = $true
                break
            }
        } catch {
            # Container not ready yet
        }
        
        $attempt++
        Write-Host "  Attempt $attempt/$maxAttempts..." -NoNewline
        Start-Sleep -Seconds 2
        Write-Host "`r" -NoNewline
    }
    
    if (-not $healthy) {
        Write-Host ""
        Write-Host "❌ API failed to become healthy after $maxAttempts attempts" -ForegroundColor Red
        Write-Host ""
        Write-Host "Container logs:" -ForegroundColor Yellow
        docker-compose logs --tail=50
        exit 1
    }
    
    Write-Host "✓ API is healthy" -ForegroundColor Green
    
    # Run tests
    Write-Host ""
    Write-Host "Running BlackBox tests..." -ForegroundColor Yellow
    Write-Host ""
    
    dotnet test src/Arcus.ClamAV.Tests/Arcus.ClamAV.Tests.csproj `
        --filter "Category=BlackBox" `
        --verbosity normal `
        --logger "console;verbosity=normal"
    
    $testExitCode = $LASTEXITCODE
    
    if ($testExitCode -eq 0) {
        Write-Host ""
        Write-Host "✓ All BlackBox tests passed!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "❌ Some tests failed" -ForegroundColor Red
    }
    
    # Cleanup
    if (-not $KeepContainers) {
        Write-Host ""
        Write-Host "Stopping containers..." -ForegroundColor Yellow
        docker-compose down
        Write-Host "✓ Containers stopped and removed" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "ℹ Containers left running (use 'docker-compose down' to stop)" -ForegroundColor Cyan
        Write-Host "  API: http://localhost:8080" -ForegroundColor Cyan
    }
    
    exit $testExitCode
    
} catch {
    Write-Host ""
    Write-Host "❌ Error: $_" -ForegroundColor Red
    
    if (-not $KeepContainers) {
        Write-Host ""
        Write-Host "Cleaning up containers..." -ForegroundColor Yellow
        docker-compose down 2>&1 | Out-Null
    }
    
    exit 1
} finally {
    Pop-Location
}
