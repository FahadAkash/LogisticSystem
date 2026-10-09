<#
.SYNOPSIS
    Distributed Logistics Platform - Unified Service Stopper
.DESCRIPTION
    Safely terminates all platform services (ASP.NET Core API/Worker, Go services, Angular frontend)
    by process tree and listening TCP ports, then removes session state files.
.PARAMETER Force
    Skips confirmation and forces termination of any listening processes on platform ports.
#>

[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = "Continue"
$RootPath = $PSScriptRoot
if (-not $RootPath) { $RootPath = Get-Location }

function Write-Header {
    param([string]$Text)
    Write-Host "`n================================================================================" -ForegroundColor Cyan
    Write-Host "   $Text" -ForegroundColor White
    Write-Host "================================================================================" -ForegroundColor Cyan
}

function Write-Success { param([string]$Text) Write-Host "[+] $Text" -ForegroundColor Green }
function Write-Warn    { param([string]$Text) Write-Host "[!] $Text" -ForegroundColor Yellow }
function Write-Info    { param([string]$Text) Write-Host "[*] $Text" -ForegroundColor Cyan }

Write-Header "STOPPING DISTRIBUTED LOGISTICS SERVICES"

$pidFile = Join-Path $RootPath ".services.json"
$stoppedCount = 0

# 1. Kill tracked processes from .services.json
if (Test-Path $pidFile) {
    Write-Info "Reading active session from .services.json..."
    try {
        $state = Get-Content $pidFile -Raw | ConvertFrom-Json
        if ($state.processes) {
            foreach ($proc in $state.processes) {
                $pidVal = $proc.pid
                $procName = $proc.name
                if ($pidVal) {
                    try {
                        $p = Get-Process -Id $pidVal -ErrorAction SilentlyContinue
                        if ($p) {
                            Write-Host "  Stopping $procName (PID: $pidVal and child processes)..." -ForegroundColor Yellow
                            # Use taskkill /T /F to terminate entire process tree (node, dotnet children, etc.)
                            $null = & taskkill /F /T /PID $pidVal 2>$null
                            $stoppedCount++
                        }
                    } catch {}
                }
            }
        }
    } catch {
        Write-Warn "Could not parse .services.json: $_"
    }

    Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
    Write-Success "Cleared session state file (.services.json)."
}

# 2. Check and clean remaining processes on platform ports
$knownPorts = @(
    @{ Port = 5229; Service = "ASP.NET Core API (dotnet-api)" },
    @{ Port = 8081; Service = "Go Location Ingest (go-ingest)" },
    @{ Port = 8082; Service = "Go Dispatch Engine (go-dispatch)" },
    @{ Port = 8083; Service = "Go Realtime Gateway (go-gateway)" },
    @{ Port = 8084; Service = "Go ETA Service (go-eta)" },
    @{ Port = 4200; Service = "Angular Frontend (angular-web)" }
)

Write-Info "Verifying platform ports are released..."
foreach ($entry in $knownPorts) {
    $port = $entry.Port
    $svc = $entry.Service
    try {
        $connections = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
        if ($connections) {
            foreach ($conn in $connections) {
                $listenPid = $conn.OwningProcess
                if ($listenPid -and $listenPid -ne 0 -and $listenPid -ne 4) {
                    Write-Warn "Port $port is still held by PID $listenPid ($svc). Terminating..."
                    $null = & taskkill /F /T /PID $listenPid 2>$null
                    $stoppedCount++
                }
            }
        }
    } catch {}
}

# 3. Double-check any orphaned dotnet-worker if running
try {
    $workerProcs = Get-CimInstance Win32_Process -Filter "CommandLine LIKE '%dotnet-worker%'" -ErrorAction SilentlyContinue
    foreach ($w in $workerProcs) {
        if ($w.ProcessId -gt 0) {
            Write-Warn "Terminating lingering dotnet-worker (PID: $($w.ProcessId))..."
            $null = & taskkill /F /T /PID $w.ProcessId 2>$null
            $stoppedCount++
        }
    }
} catch {}

Write-Host "--------------------------------------------------------------------------------" -ForegroundColor DarkCyan
if ($stoppedCount -gt 0) {
    Write-Success "All platform processes stopped successfully ($stoppedCount stopped)."
} else {
    Write-Info "No active platform processes were found running."
}
Write-Host "================================================================================`n" -ForegroundColor Cyan

