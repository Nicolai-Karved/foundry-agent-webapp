#!/usr/bin/env pwsh

param(
	[string]$BaseUrl = "http://localhost:8089",
	[string]$DocumentId = "",
	[string]$BearerToken = "",
	[string]$ClientId = "",
	[string]$Scope = "",
	[string]$PayloadOutputPath = "backend/WebApp.Api/obj/fs0002-smoke-request.json",
	[switch]$SkipRerun,
	[switch]$SkipTokenAcquisition
)

$ErrorActionPreference = "Stop"

function Import-DotEnvFile {
	param(
		[string]$Path
	)

	if (-not (Test-Path $Path)) {
		return
	}

	foreach ($line in Get-Content -Path $Path) {
		if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith("#")) {
			continue
		}

		$parts = $line.Split('=', 2, [System.StringSplitOptions]::TrimEntries)
		if ($parts.Length -ne 2) {
			continue
		}

		$key = $parts[0]
		$value = $parts[1].Trim().Trim('"').Trim("'")
		if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($key))) {
			[Environment]::SetEnvironmentVariable($key, $value)
		}
	}
}

function New-CorrelationId {
	return [Guid]::NewGuid().ToString("N")
}

function Get-ErrorBody {
	param(
		[System.Management.Automation.ErrorRecord]$ErrorRecord
	)

	$exception = $ErrorRecord.Exception
	if ($null -eq $exception.Response) {
		return $null
	}

	try {
		$stream = $exception.Response.GetResponseStream()
		if ($null -eq $stream) {
			return $null
		}

		$reader = New-Object System.IO.StreamReader($stream)
		return $reader.ReadToEnd()
	} catch {
		return $null
	}
}

function Resolve-BearerToken {
	param(
		[string]$ProvidedToken,
		[string]$ResolvedScope,
		[switch]$DoNotAcquire
	)

	if (-not [string]::IsNullOrWhiteSpace($ProvidedToken)) {
		return $ProvidedToken
	}

	if ($DoNotAcquire) {
		return ""
	}

	if ([string]::IsNullOrWhiteSpace($ResolvedScope)) {
		return ""
	}

	if ($null -eq (Get-Command az -ErrorAction SilentlyContinue)) {
		return ""
	}

	$token = az account get-access-token --scope $ResolvedScope --query accessToken -o tsv 2>$null
	if ($LASTEXITCODE -ne 0) {
		return ""
	}

	return ($token | Out-String).Trim()
}

function Get-AzureCliPrincipalType {
	if ($null -eq (Get-Command az -ErrorAction SilentlyContinue)) {
		return ""
	}

	$principalType = az account show --query user.type -o tsv 2>$null
	if ($LASTEXITCODE -ne 0) {
		return ""
	}

	return ($principalType | Out-String).Trim()
}

function Invoke-ApiJson {
	param(
		[string]$Method,
		[string]$Url,
		[string]$Token,
		[object]$Body = $null
	)

	$correlationId = New-CorrelationId
	$headers = @{
		Authorization = "Bearer $Token"
		"X-Correlation-Id" = $correlationId
	}

	$invokeParams = @{
		Method = $Method
		Uri = $Url
		Headers = $headers
		ErrorAction = "Stop"
	}

	if ($null -ne $Body) {
		$invokeParams.ContentType = "application/json"
		$invokeParams.Body = $Body | ConvertTo-Json -Depth 20
	}

	try {
		$result = Invoke-RestMethod @invokeParams
		return [pscustomobject]@{
			CorrelationId = $correlationId
			Data = $result
		}
	} catch {
		$errorBody = Get-ErrorBody -ErrorRecord $_
		Write-Host "[ERROR] $Method $Url failed. CorrelationId=$correlationId" -ForegroundColor Red
		if (-not [string]::IsNullOrWhiteSpace($errorBody)) {
			Write-Host $errorBody -ForegroundColor DarkRed
		}

		throw
	}
}

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent

Import-DotEnvFile -Path (Join-Path $projectRoot ".env")
Import-DotEnvFile -Path (Join-Path $projectRoot "backend/WebApp.Api/.env")

if ([string]::IsNullOrWhiteSpace($ClientId)) {
	$ClientId = $env:ENTRA_SPA_CLIENT_ID
}

if ([string]::IsNullOrWhiteSpace($ClientId)) {
	$ClientId = $env:AzureAd__ClientId
}

if ([string]::IsNullOrWhiteSpace($Scope) -and -not [string]::IsNullOrWhiteSpace($ClientId)) {
	$Scope = "api://$ClientId/Chat.ReadWrite"
}

$resolvedToken = Resolve-BearerToken -ProvidedToken $BearerToken -ResolvedScope $Scope -DoNotAcquire:$SkipTokenAcquisition

if ([string]::IsNullOrWhiteSpace($resolvedToken)) {
	Write-Host "[ERROR] Unable to resolve a bearer token for Chat.ReadWrite." -ForegroundColor Red
	$principalType = Get-AzureCliPrincipalType
	if ($principalType -eq "servicePrincipal") {
		Write-Host "Azure CLI is currently authenticated as a service principal, which cannot request delegated Chat.ReadWrite tokens." -ForegroundColor Yellow
		Write-Host "Use an interactive user sign-in (az logout; az login) or provide -BearerToken explicitly." -ForegroundColor Yellow
	} else {
		Write-Host "Provide -BearerToken explicitly, or make sure Azure CLI is logged in interactively and ENTRA_SPA_CLIENT_ID is available in .env." -ForegroundColor Yellow
	}
	exit 1
}

$timestamp = Get-Date -Format "yyyyMMddHHmmss"
if ([string]::IsNullOrWhiteSpace($DocumentId)) {
	$DocumentId = "smoke-word-$timestamp"
}

$now = [DateTimeOffset]::UtcNow
$payload = [pscustomobject]@{
	schemaVersion = "fs-0002/v1"
	documentId = $DocumentId
	documentVersionFingerprint = "sha256:smoke-$timestamp"
	producer = [pscustomobject]@{
		sourcePipeline = "local-smoke-test"
		serviceVersion = "local-$timestamp"
		tenantId = "local-dev"
	}
	evaluationRun = [pscustomobject]@{
		evaluationRunId = "smoke-run-$timestamp"
		startedAt = $now.AddMinutes(-1).ToString("o")
		completedAt = $now.ToString("o")
		correlationId = "smoke-corr-$timestamp"
	}
	tasks = @(
		[pscustomobject]@{
			taskId = "smoke-cc-$timestamp"
			logicalTaskKey = "smoke:content-control:$DocumentId"
			title = "Smoke test content-control task"
			description = "Validate that the Word add-in compatibility surface can load a task anchored by a deterministic content control tag."
			severity = "high"
			status = "open"
			citation = [pscustomobject]@{
				text = "Provide an explicit owner for the AIR exchange package."
				referenceSource = "Smoke standard clause 1.1"
				uri = "https://example.local/smoke/1.1"
			}
			anchor = [pscustomobject]@{
				anchorKind = "contentControlTag"
				selector = "fs0001-air-owner"
				excerpt = "The AIR exchange owner must be made explicit in the document."
				confidence = 0.98
				lastValidatedAt = $now.ToString("o")
				extensions = [pscustomobject]@{
					word = [pscustomobject]@{
						contentControlTag = "fs0001-air-owner"
					}
				}
			}
			provenance = [pscustomobject]@{
				sourcePipeline = "local-smoke-test"
				standardId = "SMOKE"
				clauseId = "1.1"
				generatedAt = $now.ToString("o")
			}
		}
		[pscustomobject]@{
			taskId = "smoke-fallback-$timestamp"
			logicalTaskKey = "smoke:text-search:$DocumentId"
			title = "Smoke test fallback-search task"
			description = "Validate that the compatibility surface returns a low-confidence fallback anchor that the Word add-in would warn about."
			severity = "medium"
			status = "open"
			citation = [pscustomobject]@{
				text = "The model exchange cadence should align with fortnightly milestones."
				referenceSource = "Smoke standard clause 2.3"
				uri = "https://example.local/smoke/2.3"
			}
			anchor = [pscustomobject]@{
				anchorKind = "textSearchFallback"
				selector = "Model exchange schedule"
				excerpt = "The document references a model exchange schedule but does not align it to fortnightly milestones."
				confidence = 0.82
				lastValidatedAt = $now.ToString("o")
				extensions = [pscustomobject]@{
					word = [pscustomobject]@{
						searchText = "Model exchange schedule"
					}
				}
			}
			provenance = [pscustomobject]@{
				sourcePipeline = "local-smoke-test"
				standardId = "SMOKE"
				clauseId = "2.3"
				generatedAt = $now.ToString("o")
			}
		}
	)
}

$payloadOutputAbsolute = if ([System.IO.Path]::IsPathRooted($PayloadOutputPath)) {
	$PayloadOutputPath
} else {
	Join-Path $projectRoot $PayloadOutputPath
}

New-Item -ItemType Directory -Force -Path (Split-Path $payloadOutputAbsolute -Parent) | Out-Null
$payload | ConvertTo-Json -Depth 20 | Set-Content -Path $payloadOutputAbsolute

$ingestUrl = "$BaseUrl/api/task-sync/ingest"
$snapshotUrl = "$BaseUrl/api/task-snapshots/$([Uri]::EscapeDataString($DocumentId))?includeSuperseded=false"
$tasksUrl = "$BaseUrl/api/tasks?documentId=$([Uri]::EscapeDataString($DocumentId))"

Write-Host "Running FS-0002 local smoke test against $BaseUrl" -ForegroundColor Cyan
Write-Host "  DocumentId: $DocumentId" -ForegroundColor Gray
Write-Host "  Scope:      $Scope" -ForegroundColor Gray
Write-Host "  Payload:    $payloadOutputAbsolute" -ForegroundColor Gray

$ingest = Invoke-ApiJson -Method POST -Url $ingestUrl -Token $resolvedToken -Body $payload
$snapshot = Invoke-ApiJson -Method GET -Url $snapshotUrl -Token $resolvedToken
$taskList = Invoke-ApiJson -Method GET -Url $tasksUrl -Token $resolvedToken

if ($taskList.Data.tasks.Count -lt 2) {
	Write-Host "[ERROR] Expected at least 2 compatibility tasks after ingest; got $($taskList.Data.tasks.Count)." -ForegroundColor Red
	exit 1
}

$firstTask = $taskList.Data.tasks[0]
$statusUpdateBody = [pscustomobject]@{
	documentId = $DocumentId
	status = "done"
	expectedVersion = [int64]$firstTask.version
}

$statusUpdateUrl = "$BaseUrl/api/tasks/$([Uri]::EscapeDataString($firstTask.taskId))/status"
$statusUpdate = Invoke-ApiJson -Method PATCH -Url $statusUpdateUrl -Token $resolvedToken -Body $statusUpdateBody

$updatedTaskList = Invoke-ApiJson -Method GET -Url $tasksUrl -Token $resolvedToken
$updatedTask = $updatedTaskList.Data.tasks | Where-Object { $_.taskId -eq $firstTask.taskId } | Select-Object -First 1

if ($null -eq $updatedTask -or $updatedTask.status -ne "done") {
	Write-Host "[ERROR] Compatibility status update did not persist as expected." -ForegroundColor Red
	exit 1
}

$citationUrl = "$BaseUrl/api/tasks/$([Uri]::EscapeDataString($firstTask.taskId))/citation-context?documentId=$([Uri]::EscapeDataString($DocumentId))"
$citation = Invoke-ApiJson -Method GET -Url $citationUrl -Token $resolvedToken

$rerun = $null
if (-not $SkipRerun) {
	$rerunUrl = "$BaseUrl/api/verification/rerun"
	$rerunBody = [pscustomobject]@{
		documentId = $DocumentId
		includeSuggestions = $true
	}

	$rerun = Invoke-ApiJson -Method POST -Url $rerunUrl -Token $resolvedToken -Body $rerunBody
	$null = Invoke-ApiJson -Method GET -Url $tasksUrl -Token $resolvedToken
}

Write-Host "`n[OK] FS-0002 smoke test passed." -ForegroundColor Green
Write-Host "  Ingest receipt:      $($ingest.Data.syncReceiptId)" -ForegroundColor Gray
Write-Host "  Evaluation run:      $($snapshot.Data.evaluationRunId)" -ForegroundColor Gray
Write-Host "  Compatibility tasks: $($taskList.Data.tasks.Count)" -ForegroundColor Gray
Write-Host "  Updated task:        $($updatedTask.taskId) -> $($updatedTask.status)" -ForegroundColor Gray
Write-Host "  Citation source:     $($citation.Data.referenceSource)" -ForegroundColor Gray

if ($null -ne $rerun) {
	Write-Host "  Rerun request:       $($rerun.Data.requestId)" -ForegroundColor Gray
}

Write-Host "`nWord-oriented smoke test anchors seeded:" -ForegroundColor Cyan
Write-Host "  Content control tag: fs0001-air-owner" -ForegroundColor Gray
Write-Host "  Fallback search text: Model exchange schedule" -ForegroundColor Gray