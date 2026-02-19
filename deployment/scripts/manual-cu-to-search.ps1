#!/usr/bin/env pwsh

param(
    [string]$SourceDir,
    [string]$AnalyzerId,
    [string]$CuEndpoint,
    [string]$AnalyzePath,
    [string]$InputContainer,
    [string]$InputPrefix,
    [string]$OutputPrefix,
    [string]$OnlyFile,
    [string]$InputJsonDir,
    [string]$IndexerName = "knowledgesource-bim-standards-indexer",
    [int]$PollSeconds = 2,
    [int]$TimeoutMinutes = 30,
    [switch]$SkipIndexer
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
if (-not $InputJsonDir -and -not (Test-Path $SourceDir)) {
    throw "Source directory not found: $SourceDir"
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) not found. Install Azure CLI to continue."
}

$documentsStorage = Get-EnvValue "DocumentsStorage"
$searchStorageConnectionString = Get-EnvValue "SEARCH_STORAGE_CONNECTION_STRING"
$searchServiceName = Get-EnvValue "SEARCH_SERVICE_NAME"
$cuResourceName = Get-EnvValue "CU_RESOURCE_NAME"
$aiProjectEndpoint = Get-EnvValue "AZURE_EXISTING_AIPROJECT_ENDPOINT"
$requestTemplate = Get-EnvValue "CU_REQUEST_TEMPLATE"
$analyzePathEnv = Get-EnvValue "CU_ANALYZE_PATH"

if (-not $AnalyzerId) { $AnalyzerId = Get-EnvValue "CU_ANALYZER_ID" }
if (-not $CuEndpoint) { $CuEndpoint = Get-EnvValue "CU_ENDPOINT" }
if (-not $AnalyzePath) { $AnalyzePath = $analyzePathEnv }
if (-not $InputContainer) { $InputContainer = Get-EnvValue "CU_INPUT_CONTAINER" }
if (-not $InputPrefix) { $InputPrefix = Get-EnvValue "CU_INPUT_PREFIX" }
if (-not $OutputPrefix) { $OutputPrefix = Get-EnvValue "CU_OUTPUT_PREFIX" }

if (-not $InputContainer) { $InputContainer = "bim-standards" }
if (-not $InputPrefix) { $InputPrefix = "source/" }
if (-not $OutputPrefix) { $OutputPrefix = "cu-output/" }

if (-not $documentsStorage) { $documentsStorage = $searchStorageConnectionString }
if (-not $documentsStorage) { throw "DocumentsStorage not set. Update .env or environment variables." }
if (-not $searchServiceName -and -not $SkipIndexer) { throw "SEARCH_SERVICE_NAME not set. Update .env or environment variables." }
if (-not $InputJsonDir) {
    if (-not $AnalyzerId) { throw "CU_ANALYZER_ID not set. Update .env or environment variables." }
    if (-not $CuEndpoint) { throw "CU_ENDPOINT not set. Update .env or environment variables." }
}
if (-not $AnalyzePath) { $AnalyzePath = "contentunderstanding/analyzers/{analyzerId}:analyze?api-version=2025-11-01" }
if (-not $requestTemplate) {
    if ($AnalyzePath -match "documentintelligence" -or $AnalyzePath -match "formrecognizer") {
        $requestTemplate = '{"urlSource":"{{blobUrl}}"}'
    } else {
        $requestTemplate = '{"input":{"url":"{{blobUrl}}"}}'
    }
}

$endpointCandidates = New-Object System.Collections.Generic.List[string]
if ($aiProjectEndpoint) {
    $endpointCandidates.Add(($aiProjectEndpoint -replace '/api/projects/.*$', ''))
}
$endpointCandidates.Add($CuEndpoint)
if ($cuResourceName) {
    $endpointCandidates.Add("https://$cuResourceName.services.ai.azure.com")
}
if ($CuEndpoint -match 'https://([^\.]+)\.cognitiveservices\.azure\.com/?') {
    $endpointCandidates.Add("https://$($Matches[1]).services.ai.azure.com")
}
$endpointCandidates = $endpointCandidates | Where-Object { $_ } | Select-Object -Unique
Write-Host "CU endpoint candidates: $($endpointCandidates -join ', ')" -ForegroundColor DarkCyan

if (-not $InputPrefix.EndsWith("/")) { $InputPrefix = "$InputPrefix/" }
if (-not $OutputPrefix.EndsWith("/")) { $OutputPrefix = "$OutputPrefix/" }

$files = @()
$analysisFiles = @()
if ($InputJsonDir) {
    if (-not (Test-Path $InputJsonDir)) { throw "InputJsonDir not found: $InputJsonDir" }
    $analysisFiles = Get-ChildItem -Path $InputJsonDir -File -Filter *.json
    if (-not $analysisFiles -or $analysisFiles.Count -eq 0) {
        throw "No JSON files found in $InputJsonDir"
    }
} else {
    $files = Get-ChildItem -Path $SourceDir -File -Filter *.pdf
    if ($OnlyFile) {
        $files = $files | Where-Object { $_.Name -eq $OnlyFile }
    }
    if (-not $files -or $files.Count -eq 0) {
        throw "No PDF files found in $SourceDir"
    }
}

$token = $null
if (-not $InputJsonDir) {
    $token = az account get-access-token --resource https://cognitiveservices.azure.com/ --query accessToken -o tsv 2>$null
    if (-not $token) { throw "Unable to acquire Azure CLI token for Content Understanding. Run 'az login' or 'azd auth login'." }
}

$analysisDir = Join-Path $repoRoot "deployment/artifacts/cu-analysis"
$jsonlDir = Join-Path $repoRoot "deployment/artifacts/cu-output"
New-Item -ItemType Directory -Force -Path $analysisDir | Out-Null
New-Item -ItemType Directory -Force -Path $jsonlDir | Out-Null

function Normalize-Markdown([string]$markdown) {
    $normalized = $markdown -replace "\r\n", "\n"
    $normalized = $normalized -replace "\r", "\n"

    # Some analyzer responses contain escaped newlines ("\\n") instead of literal newlines.
    # Decode those so paragraph splitting can produce multiple chunks.
    if (-not $normalized.Contains("`n") -and $normalized.Contains("\\n")) {
        $normalized = [regex]::Replace($normalized, "\\r\\n|\\n|\\r", "`n")
    }

    return $normalized
}

function Split-Paragraphs([string]$markdown) {
    $normalized = Normalize-Markdown $markdown
    $index = 0
    $segments = @()
    while ($index -lt $normalized.Length) {
        $breakMatch = [regex]::Match($normalized, "\n\s*\n", [System.Text.RegularExpressions.RegexOptions]::None, [TimeSpan]::FromSeconds(1))
        if ($breakMatch.Success -and $breakMatch.Index -lt $index) {
            $breakMatch = [regex]::Match($normalized.Substring($index), "\n\s*\n")
            if ($breakMatch.Success) {
                $nextBreak = $index + $breakMatch.Index
                $breakLength = $breakMatch.Length
            } else {
                $nextBreak = -1
                $breakLength = 0
            }
        } elseif ($breakMatch.Success) {
            $nextBreak = $breakMatch.Index
            $breakLength = $breakMatch.Length
        } else {
            $nextBreak = -1
            $breakLength = 0
        }

        if ($nextBreak -eq -1) {
            $segments += [pscustomobject]@{
                StartOffset = $index
                Length = $normalized.Length - $index
                Text = $normalized.Substring($index)
            }
            break
        }
        $length = $nextBreak - $index
        $segments += [pscustomobject]@{
            StartOffset = $index
            Length = $length
            Text = $normalized.Substring($index, $length)
        }
        $index = $nextBreak + $breakLength
    }
    return $segments
}

function Clean-Segment([string]$segment) {
    $lines = $segment -split "\n"
    $cleaned = $lines | Where-Object { -not $_.TrimStart().StartsWith("<!--") }
    return ($cleaned -join "\n").Trim()
}

function Get-PageSpans($contentItem) {
    $spans = @()
    if ($null -eq $contentItem.pages) { return $spans }
    foreach ($page in $contentItem.pages) {
        if ($null -eq $page.pageNumber -or $null -eq $page.spans) { continue }
        foreach ($span in $page.spans) {
            if ($null -ne $span.offset -and $null -ne $span.length) {
                $spans += [pscustomobject]@{
                    PageNumber = [int]$page.pageNumber
                    Offset = [int]$span.offset
                    Length = [int]$span.length
                }
            }
        }
    }
    return $spans
}

function Resolve-PageNumber($spans, [int]$startOffset) {
    foreach ($span in $spans) {
        if ($startOffset -ge $span.Offset -and $startOffset -lt ($span.Offset + $span.Length)) {
            return $span.PageNumber
        }
    }
    return $null
}

function Invoke-CuAnalyze([string]$url, [string]$payload, [string]$token) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    return Invoke-WebRequest -Method Post -Uri $url -Headers @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" } -Body $bytes -SkipHttpErrorCheck
}

function Get-FieldValue($field) {
    if ($null -eq $field) { return $null }
    if ($field.valueString) { return $field.valueString }
    if ($field.valueDate) { return $field.valueDate }
    if ($field.valueNumber -ne $null) { return $field.valueNumber.ToString() }
    if ($field.valueBoolean -ne $null) { return $field.valueBoolean.ToString().ToLowerInvariant() }
    if ($field.valueSelection) { return $field.valueSelection }
    if ($field.valueArray) { return ($field.valueArray | ConvertTo-Json -Compress -Depth 10) }
    if ($field.valueObject) { return ($field.valueObject | ConvertTo-Json -Compress -Depth 10) }
    return $null
}

function Get-AccountNameFromConnectionString([string]$connectionString) {
    if (-not $connectionString) { return $null }
    $match = $connectionString.Split(';') | Where-Object { $_ -match '^AccountName=' } | Select-Object -First 1
    if ($match) {
        return $match.Substring('AccountName='.Length)
    }
    return $null
}

function Normalize-DocumentKey([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $value }
    return ($value -replace '[^A-Za-z0-9_\-=]', '-')
}

$accountName = Get-AccountNameFromConnectionString $documentsStorage

foreach ($file in $files) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    Write-Host "Analyzing $($file.Name)..." -ForegroundColor Cyan

    $pdfBytes = [System.IO.File]::ReadAllBytes($file.FullName)

    Write-Host "Uploading source PDF for $($file.Name)..." -ForegroundColor Cyan
    $inputBlobName = "$InputPrefix$($file.Name)"
    az storage blob upload --connection-string $documentsStorage --container-name $InputContainer --name $inputBlobName --file $file.FullName --overwrite true | Out-Null

    $expiry = (Get-Date).ToUniversalTime().AddHours(4).ToString("yyyy-MM-ddTHH:mmZ")
    $blobSasUrl = az storage blob generate-sas --connection-string $documentsStorage --container-name $InputContainer --name $inputBlobName --permissions r --expiry $expiry --full-uri -o tsv
    if (-not $blobSasUrl) { throw "Failed to generate SAS URL for $($file.Name)" }

    $payload = $requestTemplate.Replace("{{blobUrl}}", $blobSasUrl)

    $resp = $null
    $lastError = $null
    foreach ($endpoint in $endpointCandidates) {
        $resolvedPath = $AnalyzePath.Replace("{analyzerId}", $AnalyzerId)
        $analyzeUrl = "{0}/{1}" -f $endpoint.TrimEnd('/'), $resolvedPath.TrimStart('/')
        try {
            $resp = Invoke-CuAnalyze -url $analyzeUrl -payload $payload -token $token
        } catch {
            Write-Host "CU endpoint failed: $endpoint" -ForegroundColor Yellow
            $lastError = $_.Exception.Message
            continue
        }

        if ($resp.StatusCode -ge 400) {
            $errorText = $resp.Content
            if ($errorText -and $errorText -match 'API endpoint does not match resource') {
                Write-Host "Endpoint rejected request: $endpoint" -ForegroundColor Yellow
                $lastError = $errorText
                continue
            }
            if ($errorText -and ($errorText -match "must contain a 'url' property" -or $errorText -match "urlSource" -or $errorText -match "InvalidContentLength")) {
                $alternatePayload = (@{ url = $blobSasUrl } | ConvertTo-Json -Compress)
                $resp = Invoke-CuAnalyze -url $analyzeUrl -payload $alternatePayload -token $token
                if ($resp.StatusCode -lt 400) { break }
                $errorText = $resp.Content

                $alternatePayload = (@{ urlSource = $blobSasUrl } | ConvertTo-Json -Compress)
                $resp = Invoke-CuAnalyze -url $analyzeUrl -payload $alternatePayload -token $token
                if ($resp.StatusCode -lt 400) { break }
                $errorText = $resp.Content

                $base64Payload = (@{ base64Source = [Convert]::ToBase64String($pdfBytes) } | ConvertTo-Json -Compress)
                $resp = Invoke-CuAnalyze -url $analyzeUrl -payload $base64Payload -token $token
                if ($resp.StatusCode -lt 400) { break }
                $errorText = $resp.Content
            }
            if (-not $errorText) {
                Write-Host "Endpoint returned $($resp.StatusCode) with empty body: $endpoint" -ForegroundColor Yellow
                $lastError = "Content Understanding request failed: $($resp.StatusCode) $($resp.StatusDescription)"
                continue
            }
            Write-Host "Endpoint returned $($resp.StatusCode): $endpoint" -ForegroundColor Yellow
            Write-Host $errorText -ForegroundColor DarkYellow
            $lastError = "Content Understanding request failed: $($resp.StatusCode) $($resp.StatusDescription) - $errorText"
            continue
        }

        break
    }

    if (-not $resp -or $resp.StatusCode -ge 400) {
        if (-not $lastError) { $lastError = "Content Understanding request failed for all candidate endpoints." }
        throw $lastError
    }
    $opLocation = $resp.Headers["operation-location"]
    if (-not $opLocation) { throw "Missing operation-location header for $($file.Name)" }

    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    $analysisJson = $null
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds $PollSeconds
        $pollResp = Invoke-WebRequest -Method Get -Uri $opLocation -Headers @{ Authorization = "Bearer $token" }
        $pollJson = $pollResp.Content | ConvertFrom-Json
        $status = $pollJson.status
        if ($status -eq "succeeded") {
            $analysisJson = $pollResp.Content
            break
        }
        if ($status -eq "failed") {
            throw "Content Understanding failed for $($file.Name): $($pollResp.Content)"
        }
    }

    if (-not $analysisJson) {
        throw "Timed out waiting for Content Understanding result for $($file.Name)"
    }

    $analysisPath = Join-Path $analysisDir "$baseName.cu.json"
    Set-Content -Path $analysisPath -Value $analysisJson -Encoding utf8

    $analysis = ($analysisJson | ConvertFrom-Json)
    $result = $analysis.result
    if (-not $result) { $result = $analysis.analyzeResult }
    if (-not $result) { $result = $analysis }

    $contents = $result.contents
    if (-not $contents) { throw "No contents found in CU result for $($file.Name)" }

    $jsonlPath = Join-Path $jsonlDir "$baseName.cu.jsonl"
    if (Test-Path $jsonlPath) { Remove-Item $jsonlPath -Force }

    $contentIndex = 0
    $paragraphIndex = 0
    $sectionIndex = 0
    $currentSectionId = $null
    $currentSectionTitle = $null

    $blobName = "$InputPrefix$($file.Name)"
    $sourceUrl = $null
    if ($accountName) {
        $sourceUrl = "https://$accountName.blob.core.windows.net/$InputContainer/$blobName"
    }

    foreach ($contentItem in $contents) {
        $markdown = $contentItem.markdown
        if (-not $markdown) { continue }

        $segments = Split-Paragraphs $markdown
        $pageSpans = Get-PageSpans $contentItem

        $fields = @{}
        if ($contentItem.fields) {
            foreach ($fieldName in $contentItem.fields.PSObject.Properties.Name) {
                $value = Get-FieldValue $contentItem.fields.$fieldName
                if ($null -ne $value) {
                    $fields[$fieldName] = $value
                }
            }
        }

        foreach ($segmentInfo in $segments) {
            $cleaned = Clean-Segment $segmentInfo.Text
            if ([string]::IsNullOrWhiteSpace($cleaned)) { continue }

            $paragraphIndex++
            $contentIndex++

            $chunkType = "paragraph"
            $sectionTitle = $currentSectionTitle
            $sectionId = $currentSectionId

            if ($cleaned.TrimStart() -match '^#+\s*') {
                $chunkType = "section"
                $headingMatch = [regex]::Match($cleaned.Trim(), '^#+\s*(?<id>\d+(?:\.\d+)*)?\s*(?<title>.*)$')
                if ($headingMatch.Success) {
                    $sectionId = $headingMatch.Groups['id'].Value
                    $sectionTitle = $headingMatch.Groups['title'].Value.Trim()
                    if ([string]::IsNullOrWhiteSpace($sectionTitle)) { $sectionTitle = $null }
                    if ([string]::IsNullOrWhiteSpace($sectionId)) { $sectionId = $null }
                    $currentSectionId = $sectionId
                    $currentSectionTitle = $sectionTitle
                }
            }

            $paragraphId = "p{0:0000}" -f $paragraphIndex
            $id = "{0}|{1:00}|{2}" -f $baseName, $contentIndex, $paragraphId
            $id = Normalize-DocumentKey $id
            $pageNumber = Resolve-PageNumber $pageSpans $segmentInfo.StartOffset

            $standardId = $null
            $standardTitle = $null
            if ($fields.ContainsKey("StandardNumber")) { $standardId = $fields["StandardNumber"] }
            if ($fields.ContainsKey("StandardTitle")) { $standardTitle = $fields["StandardTitle"] }

            $record = [ordered]@{
                id = $id
                content = $cleaned
                chunkType = $chunkType
                sectionId = $sectionId
                sectionTitle = $sectionTitle
                paragraphId = $paragraphId
                pageNumber = $pageNumber
                startOffset = $segmentInfo.StartOffset
                length = $segmentInfo.Length
                sourceUrl = $sourceUrl
                blobName = $blobName
                standardId = $standardId
                standardTitle = $standardTitle
            }

            foreach ($key in $fields.Keys) {
                if (-not $record.Contains($key)) {
                    $record[$key] = $fields[$key]
                }
            }

            $line = $record | ConvertTo-Json -Compress -Depth 15
            Add-Content -Path $jsonlPath -Value $line -Encoding utf8
        }
    }

    Write-Host "Uploading JSONL for $($file.Name)..." -ForegroundColor Cyan
    $outputBlobName = "$OutputPrefix$baseName.cu.jsonl"
    az storage blob upload --connection-string $documentsStorage --container-name $InputContainer --name $outputBlobName --file $jsonlPath --overwrite true | Out-Null
}

foreach ($analysisFile in $analysisFiles) {
    $analysisJson = Get-Content $analysisFile.FullName -Raw
    $baseName = $analysisFile.BaseName
    if ($baseName.EndsWith(".pdf", [System.StringComparison]::OrdinalIgnoreCase)) {
        $baseName = $baseName.Substring(0, $baseName.Length - 4)
    }
    $blobName = "$InputPrefix$baseName.pdf"
    $sourceUrl = $null
    if ($accountName) {
        $sourceUrl = "https://$accountName.blob.core.windows.net/$InputContainer/$blobName"
    }

    $analysis = ($analysisJson | ConvertFrom-Json)
    $result = $analysis.result
    if (-not $result) { $result = $analysis.analyzeResult }
    if (-not $result) { $result = $analysis }

    $contents = $result.contents
    if (-not $contents) {
        Write-Host "Skipping JSON without contents: $($analysisFile.Name)" -ForegroundColor Yellow
        continue
    }

    $jsonlPath = Join-Path $jsonlDir "$baseName.cu.jsonl"
    if (Test-Path $jsonlPath) { Remove-Item $jsonlPath -Force }

    $contentIndex = 0
    $paragraphIndex = 0
    $sectionIndex = 0
    $currentSectionId = $null
    $currentSectionTitle = $null

    foreach ($contentItem in $contents) {
        $markdown = $contentItem.markdown
        if (-not $markdown) { continue }

        $segments = Split-Paragraphs $markdown
        $pageSpans = Get-PageSpans $contentItem

        $fields = @{}
        if ($contentItem.fields) {
            foreach ($fieldName in $contentItem.fields.PSObject.Properties.Name) {
                $value = Get-FieldValue $contentItem.fields.$fieldName
                if ($null -ne $value) {
                    $fields[$fieldName] = $value
                }
            }
        }

        foreach ($segmentInfo in $segments) {
            $cleaned = Clean-Segment $segmentInfo.Text
            if ([string]::IsNullOrWhiteSpace($cleaned)) { continue }

            $paragraphIndex++
            $contentIndex++

            $chunkType = "paragraph"
            $sectionTitle = $currentSectionTitle
            $sectionId = $currentSectionId

            if ($cleaned.TrimStart() -match '^#+\s*') {
                $chunkType = "section"
                $headingMatch = [regex]::Match($cleaned.Trim(), '^#+\s*(?<id>\d+(?:\.\d+)*)?\s*(?<title>.*)$')
                if ($headingMatch.Success) {
                    $sectionId = $headingMatch.Groups['id'].Value
                    $sectionTitle = $headingMatch.Groups['title'].Value.Trim()
                    if ([string]::IsNullOrWhiteSpace($sectionTitle)) { $sectionTitle = $null }
                    if ([string]::IsNullOrWhiteSpace($sectionId)) { $sectionId = $null }
                    $currentSectionId = $sectionId
                    $currentSectionTitle = $sectionTitle
                }
            }

            $paragraphId = "p{0:0000}" -f $paragraphIndex
            $id = "{0}|{1:00}|{2}" -f $baseName, $contentIndex, $paragraphId
            $id = Normalize-DocumentKey $id
            $pageNumber = Resolve-PageNumber $pageSpans $segmentInfo.StartOffset

            $standardId = $null
            $standardTitle = $null
            if ($fields.ContainsKey("StandardNumber")) { $standardId = $fields["StandardNumber"] }
            if ($fields.ContainsKey("StandardTitle")) { $standardTitle = $fields["StandardTitle"] }

            $record = [ordered]@{
                id = $id
                content = $cleaned
                chunkType = $chunkType
                sectionId = $sectionId
                sectionTitle = $sectionTitle
                paragraphId = $paragraphId
                pageNumber = $pageNumber
                startOffset = $segmentInfo.StartOffset
                length = $segmentInfo.Length
                sourceUrl = $sourceUrl
                blobName = $blobName
                standardId = $standardId
                standardTitle = $standardTitle
            }

            foreach ($key in $fields.Keys) {
                if (-not $record.Contains($key)) {
                    $record[$key] = $fields[$key]
                }
            }

            $line = $record | ConvertTo-Json -Compress -Depth 15
            Add-Content -Path $jsonlPath -Value $line -Encoding utf8
        }
    }

    Write-Host "Uploading JSONL for $($analysisFile.Name)..." -ForegroundColor Cyan
    $outputBlobName = "$OutputPrefix$baseName.cu.jsonl"
    az storage blob upload --connection-string $documentsStorage --container-name $InputContainer --name $outputBlobName --file $jsonlPath --overwrite true | Out-Null
}

if (-not $SkipIndexer) {
    Write-Host "Triggering search indexer: $IndexerName" -ForegroundColor Cyan
    $searchToken = az account get-access-token --resource https://search.azure.com/ --query accessToken -o tsv 2>$null
    if (-not $searchToken) { throw "Unable to acquire Azure CLI token for Search. Run 'az login' or 'azd auth login'." }

    $baseUrl = "https://$searchServiceName.search.windows.net"
    $apiVersion = "2024-03-01-Preview"
    $headers = @{ "Authorization" = "Bearer $searchToken"; "Content-Type" = "application/json" }

    Invoke-RestMethod -Method Post -Uri "$baseUrl/indexers/$IndexerName/run?api-version=$apiVersion" -Headers $headers | Out-Null
    Write-Host "[OK] Indexer run requested." -ForegroundColor Green
}

Write-Host "[OK] Manual CU ingestion completed." -ForegroundColor Green
