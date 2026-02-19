#!/usr/bin/env pwsh

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

$searchServiceName = Get-EnvValue "SEARCH_SERVICE_NAME"
$resourceGroup = Get-EnvValue "AZURE_RESOURCE_GROUP_NAME"
$subscriptionId = Get-EnvValue "AZURE_SUBSCRIPTION_ID"
$storageConnectionString = Get-EnvValue "SEARCH_STORAGE_CONNECTION_STRING"
$openAiResourceUri = Get-EnvValue "AZURE_OPENAI_RESOURCE_URI"
$openAiDeploymentId = Get-EnvValue "AZURE_OPENAI_DEPLOYMENT_ID"
$openAiApiKey = Get-EnvValue "AZURE_OPENAI_API_KEY"
$searchAdminKey = Get-EnvValue "SEARCH_ADMIN_KEY"

if (-not $searchServiceName) { throw "SEARCH_SERVICE_NAME not set" }
if (-not $resourceGroup -and $searchServiceName) {
    $resourceGroup = az resource list --name $searchServiceName --query "[0].resourceGroup" -o tsv 2>$null
}
if (-not $subscriptionId) {
    $subscriptionId = az account show --query id -o tsv 2>$null
}
if (-not $resourceGroup) { throw "AZURE_RESOURCE_GROUP_NAME not set" }
if (-not $subscriptionId) { throw "AZURE_SUBSCRIPTION_ID not set" }
if (-not $storageConnectionString) { throw "SEARCH_STORAGE_CONNECTION_STRING not set" }

if (-not $openAiResourceUri) { $openAiResourceUri = "https://aif-naviate-agent-dev.openai.azure.com" }
if (-not $openAiDeploymentId) { $openAiDeploymentId = "text-embedding-3-large" }

$adminKey = $searchAdminKey
if (-not $adminKey) {
    $adminKey = az search admin-key show --service-name $searchServiceName --resource-group $resourceGroup --query primaryKey -o tsv 2>$null
}
if (-not $adminKey) { throw "Unable to fetch search admin key (set SEARCH_ADMIN_KEY to provide it)" }

$baseUrl = "https://$searchServiceName.search.windows.net"
$apiVersion = "2024-03-01-Preview"
$headers = @{ "Content-Type" = "application/json"; "api-key" = $adminKey }

$root = Split-Path -Parent $PSScriptRoot
$searchFolder = Join-Path $root "search"

$indexPath = Join-Path $searchFolder "bim-standards-paragraph-index.json"
$skillsetPath = Join-Path $searchFolder "bim-standards-paragraph-skillset.json"
$datasourcePath = Join-Path $searchFolder "knowledgesource-bim-standards-datasource.json"
$indexerPath = Join-Path $searchFolder "knowledgesource-bim-standards-indexer.json"

$indexJson = Get-Content $indexPath -Raw
$indexJson = $indexJson.Replace("__OPENAI_RESOURCE_URI__", $openAiResourceUri)
$indexJson = $indexJson.Replace("__OPENAI_DEPLOYMENT_ID__", $openAiDeploymentId)
$indexObj = $indexJson | ConvertFrom-Json
if ($indexObj.vectorSearch -and $indexObj.vectorSearch.vectorizers) {
    foreach ($vectorizer in $indexObj.vectorSearch.vectorizers) {
        if ($vectorizer.name -eq "bim-standards-vectorizer" -and $vectorizer.azureOpenAIParameters) {
            if ($openAiApiKey) {
                $vectorizer.azureOpenAIParameters.apiKey = $openAiApiKey
            } elseif ($vectorizer.azureOpenAIParameters.PSObject.Properties.Name -contains "apiKey") {
                $vectorizer.azureOpenAIParameters.PSObject.Properties.Remove("apiKey")
            }
        }
    }
}
$indexJson = $indexObj | ConvertTo-Json -Depth 25

$datasourceJson = (Get-Content $datasourcePath -Raw).Replace("__STORAGE_CONNECTION_STRING__", $storageConnectionString)
$skillsetJson = $null
$skillsetHasSkills = $false
if (Test-Path $skillsetPath) {
    $skillsetJson = Get-Content $skillsetPath -Raw
    try {
        $skillsetObj = $skillsetJson | ConvertFrom-Json
        if ($skillsetObj.skills -and $skillsetObj.skills.Count -gt 0) {
            $skillsetHasSkills = $true
        }
    } catch { }
}
$indexerJson = Get-Content $indexerPath -Raw

Write-Host "Applying search index..." -ForegroundColor Cyan
Invoke-RestMethod -Method Put -Uri "$baseUrl/indexes/bim-standards-paragraph-index?api-version=$apiVersion" -Headers $headers -Body $indexJson | Out-Null

if ($skillsetJson -and $skillsetHasSkills) {
    Write-Host "Applying skillset..." -ForegroundColor Cyan
    Invoke-RestMethod -Method Put -Uri "$baseUrl/skillsets/bim-standards-paragraph-skillset?api-version=$apiVersion" -Headers $headers -Body $skillsetJson | Out-Null
} else {
    Write-Host "Skipping skillset (not required)" -ForegroundColor Yellow
}

Write-Host "Applying datasource..." -ForegroundColor Cyan
Invoke-RestMethod -Method Put -Uri "$baseUrl/datasources/knowledgesource-bim-standards-datasource?api-version=$apiVersion" -Headers $headers -Body $datasourceJson | Out-Null

Write-Host "Applying indexer..." -ForegroundColor Cyan
Invoke-RestMethod -Method Put -Uri "$baseUrl/indexers/knowledgesource-bim-standards-indexer?api-version=$apiVersion" -Headers $headers -Body $indexerJson | Out-Null

Write-Host "[OK] Search artifacts applied" -ForegroundColor Green
