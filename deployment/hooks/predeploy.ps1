#!/usr/bin/env pwsh
# Pre-deploy: Build container (local Docker if available, ACR cloud build as fallback)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot/modules/HookLogging.ps1"
Start-HookLog -HookName "predeploy" -EnvironmentName $env:AZURE_ENV_NAME

Write-Host "Pre-Deploy: Building Container Image" -ForegroundColor Cyan

# Get required values
$clientId = azd env get-value ENTRA_SPA_CLIENT_ID 2>$null
$tenantId = azd env get-value ENTRA_TENANT_ID 2>$null
$acrName = azd env get-value AZURE_CONTAINER_REGISTRY_NAME 2>$null
$resourceGroup = azd env get-value AZURE_RESOURCE_GROUP_NAME 2>$null
$containerApp = azd env get-value AZURE_CONTAINER_APP_NAME 2>$null
$functionAppName = azd env get-value FUNCTION_APP_NAME 2>$null

if (-not $clientId -or -not $tenantId) {
    Write-Host "[ERROR] ENTRA_SPA_CLIENT_ID or ENTRA_TENANT_ID not set" -ForegroundColor Red
    exit 1
}
if (-not $acrName) {
    Write-Host "[ERROR] AZURE_CONTAINER_REGISTRY_NAME not set" -ForegroundColor Red
    exit 1
}

$imageTag = "deploy-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
$imageName = "$acrName.azurecr.io/web:$imageTag"

# Check Docker availability
$dockerAvailable = $false
if (Get-Command docker -EA SilentlyContinue) {
    $dockerVersion = docker version --format '{{.Server.Version}}' 2>$null
    if ($LASTEXITCODE -eq 0 -and $dockerVersion) {
        $dockerAvailable = $true
        Write-Host "[OK] Docker v$dockerVersion" -ForegroundColor Green
    }
}

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $projectRoot

try {
    if ($dockerAvailable) {
        Write-Host "Building with local Docker..." -ForegroundColor Cyan
        docker build --platform linux/amd64 `
            --build-arg ENTRA_SPA_CLIENT_ID=$clientId `
            --build-arg ENTRA_TENANT_ID=$tenantId `
            -f deployment/docker/frontend.Dockerfile -t $imageName . 2>&1 | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Docker build failed" }
        
        Write-Host "Pushing to ACR..." -ForegroundColor Cyan
        az acr login --name $acrName | Out-Null
        docker push $imageName 2>&1 | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Docker push failed" }
    } else {
        Write-Host "Using ACR cloud build (3-5 min)..." -ForegroundColor Yellow
        # Use --no-logs to avoid Azure CLI encoding issues on Windows with unicode characters (checkmarks)
        $buildResult = az acr build --registry $acrName --image "web:$imageTag" `
            --build-arg ENTRA_SPA_CLIENT_ID=$clientId `
            --build-arg ENTRA_TENANT_ID=$tenantId `
            --file deployment/docker/frontend.Dockerfile . `
            --no-logs --only-show-errors --output json 2>&1
        
        if ($LASTEXITCODE -ne 0) { 
            Write-Host "Build output: $buildResult" -ForegroundColor Red
            throw "ACR build failed" 
        }
        
        # Check build status from JSON result.
        # NOTE: Azure CLI can sometimes emit warnings or non-JSON text even when --output json is requested.
        # Avoid writing scary terminating ConvertFrom-Json errors into the transcript by parsing only when it looks like JSON.
        $trimmed = ($buildResult | Out-String).Trim()
        if ($trimmed.StartsWith('{') -or $trimmed.StartsWith('[')) {
            try {
                $buildJson = $trimmed | ConvertFrom-Json -ErrorAction Stop
                if ($buildJson.status -and $buildJson.status -ne "Succeeded") {
                    Write-Host "Build status: $($buildJson.status)" -ForegroundColor Red
                    throw "ACR build failed with status: $($buildJson.status)"
                }
                if ($buildJson.runId) {
                    Write-Host "Build completed (Run ID: $($buildJson.runId))" -ForegroundColor Green
                } else {
                    Write-Host "Build completed" -ForegroundColor Green
                }
            } catch {
                # If we can't parse JSON but LASTEXITCODE was 0, assume success.
                Write-Host "Build completed" -ForegroundColor Yellow
            }
        } else {
            Write-Host "Build completed" -ForegroundColor Yellow
        }
    }
    Write-Host "[OK] Image built: $imageName" -ForegroundColor Green
    
    # Update Container App (skip if doesn't exist yet - first azd up uses placeholder)
    if ($containerApp -and $resourceGroup) {
        $exists = az containerapp show --name $containerApp --resource-group $resourceGroup --query name -o tsv 2>$null
        if ($exists) {
            Write-Host "Updating Container App..." -ForegroundColor Cyan
            az containerapp update --name $containerApp --resource-group $resourceGroup --image $imageName --output none
            if ($LASTEXITCODE -ne 0) { throw "Container App update failed" }
            Write-Host "[OK] Container App updated" -ForegroundColor Green
        } else {
            Write-Host "[SKIP] Container App not yet provisioned (first run)" -ForegroundColor Yellow
        }
    }

    if ($functionAppName -and $resourceGroup) {
        $functionProject = Join-Path $projectRoot "functions/BimCuIngest/BimCuIngest.csproj"
        if (Test-Path $functionProject) {
            Write-Host "Publishing Function App..." -ForegroundColor Cyan
            $publishDir = Join-Path $projectRoot "functions/BimCuIngest/bin/Release/net8.0/publish"
            dotnet publish $functionProject -c Release -o $publishDir 2>&1 | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "Function publish failed" }

            $zipPath = Join-Path $env:TEMP "bim-cu-ingest.zip"
            if (Test-Path $zipPath) { Remove-Item $zipPath -Force -EA SilentlyContinue }
            Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force

            az functionapp deployment source config-zip --name $functionAppName --resource-group $resourceGroup --src $zipPath --output none
            if ($LASTEXITCODE -ne 0) { throw "Function App deployment failed" }
            Write-Host "[OK] Function App deployed" -ForegroundColor Green
        } else {
            Write-Host "[SKIP] Function project not found at $functionProject" -ForegroundColor Yellow
        }
    }
    
    azd env set SERVICE_WEB_IMAGE_NAME $imageName 2>$null
} finally {
    Pop-Location
}

if ($script:HookLogFile) {
    Write-Host "[LOG] Log file: $script:HookLogFile" -ForegroundColor DarkGray
}
Stop-HookLog
