#!/usr/bin/env pwsh
param(
    [string]$ProjectEndpoint,
    [string]$Model,
    [switch]$UpdateExisting,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectEndpoint)) { $ProjectEndpoint = $env:AI_AGENT_ENDPOINT }
if ([string]::IsNullOrWhiteSpace($ProjectEndpoint)) {
    $ProjectEndpoint = (azd env get-value AI_AGENT_ENDPOINT 2>&1) | Where-Object { $_ -notmatch 'ERROR|WARNING' } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($ProjectEndpoint)) {
    throw 'AI_AGENT_ENDPOINT not found. Set env var or run azd env set AI_AGENT_ENDPOINT <endpoint>'
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$promptsPath = Join-Path $repoRoot 'docs/agent-prompts'
$sharedPromptFile = Join-Path $promptsPath '_shared-core.md'

$tokenData = az account get-access-token --resource 'https://ai.azure.com' 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    throw "Failed to get Azure token: $tokenData"
}
$accessToken = ($tokenData | ConvertFrom-Json).accessToken

$agents = & "$PSScriptRoot\..\hooks\modules\Get-AIFoundryAgents.ps1" -ProjectEndpoint $ProjectEndpoint -AccessToken $accessToken -Quiet
$existingNames = @($agents | ForEach-Object { $_.name })

if ([string]::IsNullOrWhiteSpace($Model)) {
    $default = $agents | Where-Object { $_.name -eq 'generic-bim' } | Select-Object -First 1
    $Model = $default.versions.latest.definition.model
}
if ([string]::IsNullOrWhiteSpace($Model)) { $Model = 'gpt-5.2' }

if (-not (Test-Path $sharedPromptFile)) {
    throw "Shared prompt file not found: $sharedPromptFile"
}

$sharedInstructions = (Get-Content -Raw -Path $sharedPromptFile).Trim()

$specs = @(
    @{
        Name = 'standard-compliance-checker'
        PromptFile = Join-Path $promptsPath 'standard-compliance-checker.md'
        StarterPrompts = 'Validate this AIR against selected standards|Validate this EIR against selected standards'
    },
    @{
        Name = 'general-bim-standard-qa'
        PromptFile = Join-Path $promptsPath 'general-bim-standard-qa.md'
        StarterPrompts = 'Explain ISO 19650 compliance expectations|What information is missing from this BIM handover context?'
    },
    @{
        Name = 'document-compliance-checker'
        PromptFile = Join-Path $promptsPath 'document-compliance-checker.md'
        StarterPrompts = 'Compare this BEP against AIR and EIR|Identify conflicts between these BIM documents'
    }
)

function Invoke-FoundryApi {
    param(
        [string]$Method,
        [string]$Url,
        $Body
    )

    $headers = @{ Authorization = "Bearer $accessToken" }
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $Url -Headers $headers
    }

    $jsonBody = $Body | ConvertTo-Json -Depth 20 -Compress
    return Invoke-RestMethod -Method $Method -Uri $Url -Headers $headers -ContentType 'application/json' -Body $jsonBody
}

Write-Host "Endpoint: $ProjectEndpoint" -ForegroundColor Cyan
Write-Host "Model: $Model" -ForegroundColor Cyan

foreach ($spec in $specs) {
    $name = $spec.Name
    $promptFile = $spec.PromptFile

    if (-not (Test-Path $promptFile)) {
        throw "Prompt file not found: $promptFile"
    }

    $agentSpecificInstructions = (Get-Content -Raw -Path $promptFile).Trim()
    $instructions = @(
        $sharedInstructions
        ''
        $agentSpecificInstructions
    ) -join [Environment]::NewLine

    $payload = @{
        name = $name
        definition = @{
            kind = 'prompt'
            model = $Model
            instructions = $instructions
            tools = @()
        }
        metadata = @{
            description = ''
            starterPrompts = $spec.StarterPrompts
            logo = 'Avatar_Default.svg'
        }
    }

    $exists = $existingNames -contains $name

    if ($exists -and -not $UpdateExisting) {
        Write-Host "[SKIP] $name exists (use -UpdateExisting to create a new version)." -ForegroundColor Yellow
        continue
    }

    if ($DryRun) {
        $action = if ($exists) { 'UPDATE' } else { 'CREATE' }
        Write-Host "[DRY-RUN] $action $name" -ForegroundColor DarkYellow
        continue
    }

    if (-not $exists) {
        $createUrl = "$ProjectEndpoint/agents?api-version=2025-11-15-preview"
        try {
            $null = Invoke-FoundryApi -Method 'POST' -Url $createUrl -Body $payload
            Write-Host "[OK] Created $name" -ForegroundColor Green
            continue
        }
        catch {
            Write-Host "[WARN] Create failed for $name via /agents POST. Attempting upsert version endpoint..." -ForegroundColor Yellow
        }
    }

    $versionUrl = "$ProjectEndpoint/agents/$name/versions?api-version=2025-11-15-preview"
    $versionPayload = @{
        definition = $payload.definition
        metadata = $payload.metadata
        description = ''
    }

    $null = Invoke-FoundryApi -Method 'POST' -Url $versionUrl -Body $versionPayload
    Write-Host "[OK] Updated $name (new version)" -ForegroundColor Green
}

Write-Host "Done." -ForegroundColor Green
