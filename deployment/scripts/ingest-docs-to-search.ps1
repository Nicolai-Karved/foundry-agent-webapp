#!/usr/bin/env pwsh

param(
    [string]$SourceDir,
    [int]$PollSeconds = 10,
    [int]$TimeoutMinutes = 30,
    [string]$InputContainer,
    [string]$InputPrefix,
    [string]$OutputPrefix,
    [string]$IndexerName = "knowledgesource-bim-standards-indexer"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$envFile = Join-Path $repoRoot ".env"
if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()
        if (-not [string]::IsNullOrWhiteSpace($line) -and -not $line.StartsWith("#")) {
            $parts = $line.Split("=", 2)
            if ($parts.Length -eq 2 -and -not [Environment]::GetEnvironmentVariable($parts[0].Trim())) {
                $value = $parts[1].Trim().Trim('"')
                [Environment]::SetEnvironmentVariable($parts[0].Trim(), $value)
            }
        }
    }
}

function Get-EnvValue([string]$name) {
    $value = (azd env get-value $name 2>&1) | Where-Object { $_ -notmatch 'ERROR' } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($value)) { return [Environment]::GetEnvironmentVariable($name) }
    return $value
}

if (-not $SourceDir) {
    $SourceDir = Join-Path $repoRoot "docs/sources"
}

if (-not (Test-Path $SourceDir)) {
    throw "Source directory not found: $SourceDir"
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) not found. Install Azure CLI to continue."
}

$documentsStorage = Get-EnvValue "DocumentsStorage"
$searchStorageConnectionString = Get-EnvValue "SEARCH_STORAGE_CONNECTION_STRING"
$searchServiceName = Get-EnvValue "SEARCH_SERVICE_NAME"

if (-not $InputContainer) { $InputContainer = Get-EnvValue "CU_INPUT_CONTAINER" }
if (-not $InputPrefix) { $InputPrefix = Get-EnvValue "CU_INPUT_PREFIX" }
if (-not $OutputPrefix) { $OutputPrefix = Get-EnvValue "CU_OUTPUT_PREFIX" }

if (-not $InputContainer) { $InputContainer = "bim-standards" }
if (-not $InputPrefix) { $InputPrefix = "source/" }
if (-not $OutputPrefix) { $OutputPrefix = "cu-output/" }

if (-not $documentsStorage) { $documentsStorage = $searchStorageConnectionString }
if (-not $documentsStorage) { throw "DocumentsStorage not set. Update .env or environment variables." }
if (-not $searchServiceName) { throw "SEARCH_SERVICE_NAME not set. Update .env or environment variables." }

if (-not $InputPrefix.EndsWith("/")) { $InputPrefix = "$InputPrefix/" }
if (-not $OutputPrefix.EndsWith("/")) { $OutputPrefix = "$OutputPrefix/" }

$files = Get-ChildItem -Path $SourceDir -File -Filter *.pdf
if (-not $files -or $files.Count -eq 0) {
    throw "No PDF files found in $SourceDir"
}

Write-Host "Uploading PDFs from $SourceDir to $InputContainer/$InputPrefix" -ForegroundColor Cyan
az storage blob upload-batch `
    --connection-string $documentsStorage `
    --destination $InputContainer `
    --destination-path $InputPrefix `
    --source $SourceDir `
    --pattern "*.pdf" `
    --overwrite | Out-Null

$expectedBlobs = @()
foreach ($file in $files) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $expectedBlobs += "$OutputPrefix$baseName.cu.jsonl"
}

Write-Host "Waiting for JSONL outputs in $InputContainer/$OutputPrefix" -ForegroundColor Cyan
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
$missing = $expectedBlobs
while ($missing.Count -gt 0 -and (Get-Date) -lt $deadline) {
    $stillMissing = @()
    foreach ($blobName in $missing) {
        $exists = az storage blob exists --connection-string $documentsStorage --container-name $InputContainer --name $blobName --query exists -o tsv 2>$null
        if ($exists -ne "true") {
            $stillMissing += $blobName
        }
    }

    $missing = $stillMissing
    if ($missing.Count -gt 0) {
        Write-Host "Waiting on $($missing.Count) JSONL blob(s)..." -ForegroundColor Yellow
        Start-Sleep -Seconds $PollSeconds
    }
}

if ($missing.Count -gt 0) {
    $missingList = $missing -join ", "
    throw "Timed out waiting for JSONL outputs: $missingList"
}

Write-Host "Triggering search indexer: $IndexerName" -ForegroundColor Cyan
$token = az account get-access-token --resource https://search.azure.com/ --query accessToken -o tsv 2>$null
if (-not $token) { throw "Unable to acquire Azure CLI token for Search. Run 'az login' or 'azd auth login'." }

$baseUrl = "https://$searchServiceName.search.windows.net"
$apiVersion = "2024-03-01-Preview"
$headers = @{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" }

Invoke-RestMethod -Method Post -Uri "$baseUrl/indexers/$IndexerName/run?api-version=$apiVersion" -Headers $headers | Out-Null

Write-Host "[OK] Ingestion complete. Indexer run requested." -ForegroundColor Green
Write-Host "Note: Ensure the local Functions host is running so cu-output JSONL gets created." -ForegroundColor Yellow
