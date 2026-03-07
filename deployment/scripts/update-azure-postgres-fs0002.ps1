#!/usr/bin/env pwsh

param(
	[string]$ConnectionString = "",
	[string]$SqlOutputPath = "backend/WebApp.Api/obj/fs0002-idempotent.sql",
	[switch]$SkipToolRestore,
	[switch]$SkipSqlGeneration,
	[switch]$UseDockerPsql
)

$ErrorActionPreference = "Stop"

function Get-ConnectionValue {
	param(
		[hashtable]$Values,
		[string[]]$Keys,
		[string]$DefaultValue = ""
	)

	foreach ($key in $Keys) {
		if ($Values.ContainsKey($key)) {
			return [string]$Values[$key]
		}
	}

	return $DefaultValue
}

function ConvertFrom-ConnectionString {
	param(
		[string]$ConnectionString
	)

	$values = @{}
	$segments = $ConnectionString -split ';'
	foreach ($segment in $segments) {
		if ([string]::IsNullOrWhiteSpace($segment)) {
			continue
		}

		$keyValue = $segment.Split('=', 2, [System.StringSplitOptions]::TrimEntries)
		if ($keyValue.Length -ne 2) {
			continue
		}

		$key = $keyValue[0].Trim()
		$value = $keyValue[1].Trim().Trim('"').Trim("'")
		$values[$key] = $value
	}

	return $values
}

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sqlPath = if ([System.IO.Path]::IsPathRooted($SqlOutputPath)) { $SqlOutputPath } else { Join-Path $projectRoot $SqlOutputPath }
$sqlDirectory = Split-Path $sqlPath -Parent
$sqlFileName = Split-Path $sqlPath -Leaf

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
	$ConnectionString = $env:AZURE_FS0002_POSTGRES_CONNECTION_STRING
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
	Write-Host "[ERROR] Provide -ConnectionString or set AZURE_FS0002_POSTGRES_CONNECTION_STRING." -ForegroundColor Red
	exit 1
}

if (-not $SkipToolRestore) {
	Push-Location $projectRoot
	try {
		dotnet tool restore
		if ($LASTEXITCODE -ne 0) {
			Write-Host "[ERROR] dotnet tool restore failed." -ForegroundColor Red
			exit $LASTEXITCODE
		}
	} finally {
		Pop-Location
	}
}

if (-not $SkipSqlGeneration) {
	New-Item -ItemType Directory -Force -Path $sqlDirectory | Out-Null
	Push-Location $projectRoot
	try {
		dotnet ef migrations script --project backend/WebApp.Api/WebApp.Api.csproj --startup-project backend/WebApp.Api/WebApp.Api.csproj --context EvaluationTaskDbContext --idempotent --output $sqlPath
		if ($LASTEXITCODE -ne 0) {
			Write-Host "[ERROR] Failed to generate idempotent SQL migration script." -ForegroundColor Red
			exit $LASTEXITCODE
		}
	} finally {
		Pop-Location
	}
}

if (-not (Test-Path $sqlPath)) {
	Write-Host "[ERROR] SQL migration script not found at: $sqlPath" -ForegroundColor Red
	exit 1
}

$connectionValues = ConvertFrom-ConnectionString -ConnectionString $ConnectionString

$hostName = Get-ConnectionValue -Values $connectionValues -Keys @("Host", "Server", "Data Source")
$port = Get-ConnectionValue -Values $connectionValues -Keys @("Port") -DefaultValue "5432"
$database = Get-ConnectionValue -Values $connectionValues -Keys @("Database", "Initial Catalog")
$username = Get-ConnectionValue -Values $connectionValues -Keys @("Username", "User Name", "User ID", "UID")
$password = Get-ConnectionValue -Values $connectionValues -Keys @("Password", "PWD")
$sslMode = (Get-ConnectionValue -Values $connectionValues -Keys @("SSL Mode", "SslMode") -DefaultValue "Require").ToLowerInvariant()

if ([string]::IsNullOrWhiteSpace($hostName) -or [string]::IsNullOrWhiteSpace($database) -or [string]::IsNullOrWhiteSpace($username)) {
	Write-Host "[ERROR] Connection string must include host, database, and username." -ForegroundColor Red
	exit 1
}

$psqlConnection = "host=$hostName port=$port dbname=$database user=$username sslmode=$sslMode"

Write-Host "Applying FS-0002 migration script to PostgreSQL host '$hostName' database '$database'..." -ForegroundColor Cyan

$hasLocalPsql = $null -ne (Get-Command psql -ErrorAction SilentlyContinue)

if ($hasLocalPsql -and -not $UseDockerPsql) {
	$previousPassword = $env:PGPASSWORD
	try {
		$env:PGPASSWORD = $password
		& psql $psqlConnection -v ON_ERROR_STOP=1 -f $sqlPath
		if ($LASTEXITCODE -ne 0) {
			Write-Host "[ERROR] psql failed while applying the migration script." -ForegroundColor Red
			exit $LASTEXITCODE
		}
	} finally {
		$env:PGPASSWORD = $previousPassword
	}
}
else {
	if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
		Write-Host "[ERROR] Neither psql nor docker is available to run the migration script." -ForegroundColor Red
		exit 1
	}

	$dockerHostName = switch -Regex ($hostName) {
		'^(localhost|127\.0\.0\.1|::1)$' { 'host.docker.internal'; break }
		default { $hostName; break }
	}
	$dockerPsqlConnection = "host=$dockerHostName port=$port dbname=$database user=$username sslmode=$sslMode"

	docker run --rm -e PGPASSWORD=$password -v "${sqlDirectory}:/work" postgres:16-alpine psql $dockerPsqlConnection -v ON_ERROR_STOP=1 -f "/work/$sqlFileName"
	if ($LASTEXITCODE -ne 0) {
		Write-Host "[ERROR] Dockerized psql failed while applying the migration script." -ForegroundColor Red
		exit $LASTEXITCODE
	}
}

Write-Host "[OK] FS-0002 migration script applied successfully." -ForegroundColor Green
Write-Host "  SQL script: $sqlPath" -ForegroundColor Gray