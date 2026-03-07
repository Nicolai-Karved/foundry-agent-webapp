#!/usr/bin/env pwsh
# Starts local dev using Docker Compose (backend + frontend with hot reload)

param(
    [switch]$SkipBrowser,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$composeFile = Join-Path $projectRoot "docker-compose.dev.yml"

if (-not (Test-Path $composeFile)) {
    Write-Host "[ERROR] docker-compose.dev.yml not found at: $composeFile" -ForegroundColor Red
    exit 1
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] Docker CLI not found. Install Docker Desktop first." -ForegroundColor Red
    exit 1
}

# Validate Docker daemon availability
try {
    docker info | Out-Null
} catch {
    Write-Host "[ERROR] Docker daemon is not running. Start Docker Desktop and retry." -ForegroundColor Red
    exit 1
}

Push-Location $projectRoot
try {
    if ($NoBuild) {
        docker compose -f $composeFile up -d
    } else {
        docker compose -f $composeFile up -d --build
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Docker Compose failed to start services." -ForegroundColor Red
        exit $LASTEXITCODE
    }
} finally {
    Pop-Location
}

Start-Sleep -Seconds 3

if (-not $SkipBrowser) {
    if ($IsWindows) { Start-Process "http://localhost:5173" }
    elseif ($IsMacOS) { open "http://localhost:5173" }
    elseif (Get-Command xdg-open -ErrorAction SilentlyContinue) { xdg-open "http://localhost:5173" }
}

$localPostgresDb = if ([string]::IsNullOrWhiteSpace($env:FS0002_LOCAL_POSTGRES_DB)) { "foundry_agent_fs0002" } else { $env:FS0002_LOCAL_POSTGRES_DB }
$localPostgresUser = if ([string]::IsNullOrWhiteSpace($env:FS0002_LOCAL_POSTGRES_USER)) { "postgres" } else { $env:FS0002_LOCAL_POSTGRES_USER }

Write-Host "`n[OK] Docker dev services started" -ForegroundColor Green
Write-Host "  Frontend: http://localhost:5173" -ForegroundColor Cyan
Write-Host "  Backend:  http://localhost:8089" -ForegroundColor Cyan
Write-Host "  Postgres: localhost:5432 (db=$localPostgresDb user=$localPostgresUser)" -ForegroundColor Cyan
Write-Host "`nUseful commands:" -ForegroundColor Gray
Write-Host "  docker compose -f docker-compose.dev.yml logs -f" -ForegroundColor Gray
Write-Host "  docker compose -f docker-compose.dev.yml down" -ForegroundColor Gray
