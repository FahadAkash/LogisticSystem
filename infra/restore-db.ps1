<#
.SYNOPSIS
    Database restore script for PostgreSQL running in Docker on Windows.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [string]$ContainerName = "logistic-postgres",
    [string]$DbName = "appdb",
    [string]$DbUser = "postgres"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupFile)) {
    Write-Host "[-] Backup file not found: $BackupFile" -ForegroundColor Red
    exit 1
}

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host " Restoring PostgreSQL Database: $DbName" -ForegroundColor Yellow
Write-Host " File: $BackupFile" -ForegroundColor Gray
Write-Host "==================================================================" -ForegroundColor Cyan

Get-Content -Path $BackupFile | docker exec -i $ContainerName psql -U $DbUser -d $DbName

Write-Host "[+] Restore completed successfully!" -ForegroundColor Green
