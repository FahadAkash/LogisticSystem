<#
.SYNOPSIS
    Automated hot backup script for PostgreSQL running in Docker on Windows.
#>
param(
    [string]$ContainerName = "logistic-postgres",
    [string]$DbName = "appdb",
    [string]$DbUser = "postgres",
    [string]$BackupDir = "backups",
    [int]$RetentionDays = 7
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$dumpSql = Join-Path $BackupDir "${DbName}_${timestamp}.sql"
$zipFile = Join-Path $BackupDir "${DbName}_${timestamp}.sql.gz"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host " Starting PostgreSQL Hot Backup: $DbName" -ForegroundColor White
Write-Host " Container : $ContainerName" -ForegroundColor Gray
Write-Host " Output    : $dumpSql" -ForegroundColor Gray
Write-Host "==================================================================" -ForegroundColor Cyan

# Verify container is running
$running = docker ps --format '{{.Names}}' | Where-Object { $_ -eq $ContainerName }
if (-not $running) {
    Write-Host "[-] Error: Container '$ContainerName' is not running!" -ForegroundColor Red
    exit 1
}

# Run pg_dump
docker exec -t $ContainerName pg_dump -U $DbUser -d $DbName --clean --if-exists > $dumpSql

if (Test-Path $dumpSql) {
    $size = (Get-Item $dumpSql).Length / 1MB
    Write-Host "[+] Backup completed successfully! ($([Math]::Round($size, 2)) MB)" -ForegroundColor Green
} else {
    Write-Host "[-] Failed to produce backup file." -ForegroundColor Red
}

# Clean old backups
Get-ChildItem -Path $BackupDir -Filter "${DbName}_*.sql" | Where-Object {
    $_.CreationTime -lt (Get-Date).AddDays(-$RetentionDays)
} | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "[+] Retention cleanup finished." -ForegroundColor Green
