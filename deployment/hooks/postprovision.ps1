#!/usr/bin/env pwsh
# Post-provision: Updates Entra app redirect URIs and assigns RBAC to AI Foundry resource

$ErrorActionPreference = "Stop"
. "$PSScriptRoot/modules/HookLogging.ps1"
Start-HookLog -HookName "postprovision" -EnvironmentName $env:AZURE_ENV_NAME

Write-Host "Post-Provision: Configure Entra App & RBAC" -ForegroundColor Cyan

# Get required env vars
$clientId = azd env get-value ENTRA_SPA_CLIENT_ID 2>$null
$containerAppUrl = azd env get-value WEB_ENDPOINT 2>$null
$webIdentityPrincipalId = azd env get-value WEB_IDENTITY_PRINCIPAL_ID 2>$null
$functionIdentityPrincipalId = azd env get-value FUNCTION_IDENTITY_PRINCIPAL_ID 2>$null
$functionStorageAccountName = azd env get-value FUNCTION_STORAGE_ACCOUNT_NAME 2>$null
$documentsStorageAccountName = azd env get-value DOCUMENTS_STORAGE_ACCOUNT_NAME 2>$null
$cuResourceName = azd env get-value CU_RESOURCE_NAME 2>$null
$resourceGroup = azd env get-value AZURE_RESOURCE_GROUP_NAME 2>$null
$aiFoundryResourceGroup = azd env get-value AI_FOUNDRY_RESOURCE_GROUP 2>$null
$aiFoundryResourceName = azd env get-value AI_FOUNDRY_RESOURCE_NAME 2>$null
$subscriptionId = azd env get-value AZURE_SUBSCRIPTION_ID 2>$null

if (-not $clientId) {
    Write-Host "[ERROR] ENTRA_SPA_CLIENT_ID not set" -ForegroundColor Red
    exit 1
}
if (-not $containerAppUrl) {
    Write-Host "[ERROR] WEB_ENDPOINT not set" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Container App: $containerAppUrl" -ForegroundColor Green

# Update Entra app redirect URIs
$app = az ad app show --id $clientId | ConvertFrom-Json
$redirectUris = @(
    "http://localhost:8080",
    "http://localhost:5173",
    $containerAppUrl
)

$spaBody = @{ spa = @{ redirectUris = $redirectUris } } | ConvertTo-Json -Depth 10
$tempFile = [System.IO.Path]::GetTempFileName()
$spaBody | Out-File -FilePath $tempFile -Encoding utf8

az rest --method PATCH `
    --uri "https://graph.microsoft.com/v1.0/applications/$($app.id)" `
    --headers "Content-Type=application/json" `
    --body "@$tempFile" | Out-Null

Remove-Item $tempFile -EA SilentlyContinue

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Failed to update Entra app" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Redirect URIs updated" -ForegroundColor Green
$redirectUris | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }

# Assign Azure AI User role to web managed identity on AI Foundry resource
# This is done via Azure CLI (not Bicep) to prevent azd from tracking the external resource group
if ($webIdentityPrincipalId -and $aiFoundryResourceGroup -and $aiFoundryResourceName -and $subscriptionId) {
    Write-Host "Assigning Azure AI User role to web app identity..." -ForegroundColor Yellow
    
    $scope = "/subscriptions/$subscriptionId/resourceGroups/$aiFoundryResourceGroup/providers/Microsoft.CognitiveServices/accounts/$aiFoundryResourceName"
    
    # Check if role assignment already exists
    $existingAssignment = az role assignment list `
        --assignee $webIdentityPrincipalId `
        --role "Azure AI User" `
        --scope $scope 2>$null | ConvertFrom-Json
    
    if ($existingAssignment -and $existingAssignment.Count -gt 0) {
        Write-Host "[OK] Role assignment already exists" -ForegroundColor Green
    } else {
        az role assignment create `
            --assignee-object-id $webIdentityPrincipalId `
            --assignee-principal-type ServicePrincipal `
            --role "Azure AI User" `
            --scope $scope | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Role assignment created on AI Foundry resource" -ForegroundColor Green
        } else {
            Write-Host "[WARN] Failed to create role assignment - you may need to do this manually" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "[SKIP] AI Foundry role assignment - missing configuration" -ForegroundColor Yellow
    Write-Host "  Set AI_FOUNDRY_RESOURCE_GROUP and AI_FOUNDRY_RESOURCE_NAME environment variables" -ForegroundColor Gray
}

# Assign storage access to the CU ingest Function identity
if ($functionIdentityPrincipalId -and $subscriptionId -and $resourceGroup) {
    if ($functionStorageAccountName) {
        $funcStorageScope = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Storage/storageAccounts/$functionStorageAccountName"
        $existingFuncStorage = az role assignment list `
            --assignee $functionIdentityPrincipalId `
            --role "Storage Blob Data Contributor" `
            --scope $funcStorageScope 2>$null | ConvertFrom-Json
        if (-not $existingFuncStorage -or $existingFuncStorage.Count -eq 0) {
            az role assignment create `
                --assignee-object-id $functionIdentityPrincipalId `
                --assignee-principal-type ServicePrincipal `
                --role "Storage Blob Data Contributor" `
                --scope $funcStorageScope | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "[OK] Function identity granted access to function storage" -ForegroundColor Green
            } else {
                Write-Host "[WARN] Failed to grant access to function storage" -ForegroundColor Yellow
            }
        }
    }

    if ($documentsStorageAccountName) {
        $docsStorageScope = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Storage/storageAccounts/$documentsStorageAccountName"
        $existingDocsStorage = az role assignment list `
            --assignee $functionIdentityPrincipalId `
            --role "Storage Blob Data Contributor" `
            --scope $docsStorageScope 2>$null | ConvertFrom-Json
        if (-not $existingDocsStorage -or $existingDocsStorage.Count -eq 0) {
            az role assignment create `
                --assignee-object-id $functionIdentityPrincipalId `
                --assignee-principal-type ServicePrincipal `
                --role "Storage Blob Data Contributor" `
                --scope $docsStorageScope | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "[OK] Function identity granted access to documents storage" -ForegroundColor Green
            } else {
                Write-Host "[WARN] Failed to grant access to documents storage" -ForegroundColor Yellow
            }
        }
    }
} else {
    Write-Host "[SKIP] Function storage RBAC - missing configuration" -ForegroundColor Yellow
}

# Assign Cognitive Services User role to the CU ingest Function identity
if ($functionIdentityPrincipalId -and $subscriptionId -and $resourceGroup -and $cuResourceName) {
    $cuScope = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.CognitiveServices/accounts/$cuResourceName"
    $existingCuAssignment = az role assignment list `
        --assignee $functionIdentityPrincipalId `
        --role "Cognitive Services User" `
        --scope $cuScope 2>$null | ConvertFrom-Json
    if (-not $existingCuAssignment -or $existingCuAssignment.Count -eq 0) {
        az role assignment create `
            --assignee-object-id $functionIdentityPrincipalId `
            --assignee-principal-type ServicePrincipal `
            --role "Cognitive Services User" `
            --scope $cuScope | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Function identity granted access to Content Understanding resource" -ForegroundColor Green
        } else {
            Write-Host "[WARN] Failed to grant access to Content Understanding resource" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "[SKIP] CU RBAC - missing CU_RESOURCE_NAME or function identity" -ForegroundColor Yellow
}

# Open browser
try { Start-Process $containerAppUrl } catch { }

Write-Host "[OK] Post-provision complete. URL: $containerAppUrl" -ForegroundColor Green

if ($script:HookLogFile) {
    Write-Host "[LOG] Log file: $script:HookLogFile" -ForegroundColor DarkGray
}
Stop-HookLog
