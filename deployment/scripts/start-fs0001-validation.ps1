#!/usr/bin/env pwsh
# FS-0001 manual validation helper
# Starts local dev servers and prints links to validation artifacts.

param(
    [switch]$SkipBrowser
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$startLocalScript = Join-Path $projectRoot "deployment/scripts/start-local-dev.ps1"

if (-not (Test-Path $startLocalScript)) {
    Write-Host "[ERROR] Could not find start-local-dev.ps1" -ForegroundColor Red
    exit 1
}

Write-Host "Starting local dev servers for FS-0001 validation..." -ForegroundColor Cyan

if ($SkipBrowser) {
    & $startLocalScript -SkipBrowser
} else {
    & $startLocalScript
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Failed to start local dev servers." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`nFS-0001 validation artifacts:" -ForegroundColor Green
Write-Host "  Matrix:    docs/testing/fs-0001-word-host-validation-matrix.md" -ForegroundColor Cyan
Write-Host "  Guide:     docs/testing/fs-0001-word-host-validation-execution.md" -ForegroundColor Cyan
Write-Host "  Evidence:  docs/testing/fs-0001-word-host-validation-evidence.csv" -ForegroundColor Cyan
