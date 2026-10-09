<#
.SYNOPSIS
    Distributed Logistics Platform - Unified Service Runner
.DESCRIPTION
    Starts all platform services (ASP.NET Core API, Worker, 4 Golang services, and Angular Frontend),
    detects local and network LAN IPs, and displays a live connection and health dashboard.
.PARAMETER Windowed
    Opens each service in its own dedicated console window for real-time log viewing.
.PARAMETER NoFrontend
    Skips starting the Angular frontend (runs backend services only).
.PARAMETER Detach
    Runs all services in background and returns immediately without keeping interactive monitor session open.
.PARAMETER TimeoutSeconds
    Maximum seconds to wait for initial health checks (default: 30).
#>

[CmdletBinding()]
param(
    [switch]$Windowed,
    [switch]$NoFrontend,
    [switch]$Detach,
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Continue"
$RootPath = $PSScriptRoot
if (-not $RootPath) { $RootPath = Get-Location }

# 1. Colors and UI Helpers
function Write-Header {
    param([string]$Text)
    Write-Host "`n================================================================================" -ForegroundColor Cyan
    Write-Host "   $Text" -ForegroundColor White
    Write-Host "================================================================================" -ForegroundColor Cyan
}

function Write-Success { param([string]$Text) Write-Host "[+] $Text" -ForegroundColor Green }
function Write-Warn    { param([string]$Text) Write-Host "[!] $Text" -ForegroundColor Yellow }
function Write-Err     { param([string]$Text) Write-Host "[-] $Text" -ForegroundColor Red }
function Write-Info    { param([string]$Text) Write-Host "[*] $Text" -ForegroundColor Cyan }

Clear-Host
Write-Header "DISTRIBUTED LOGISTICS & DISPATCH PLATFORM - LAUNCHER"

# 2. Detect Local and LAN Network IP Addresses
Write-Info "Detecting network configuration..."

$lanIp = $null
try {
    $lanIp = (Get-NetIPConfiguration -ErrorAction SilentlyContinue | Where-Object { $_.IPv4DefaultGateway -ne $null }).IPv4Address.IPAddress | Select-Object -First 1
} catch {}

if (-not $lanIp) {
    try {
        $lanIp = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { 
            $_.IPAddress -notlike "127.*" -and 
            $_.IPAddress -notlike "169.254*" 
        } | Select-Object -First 1).IPAddress
    } catch {}
}

if (-not $lanIp) { $lanIp = "127.0.0.1" }

$allIps = @()
try {
    $allIps = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { 
        $_.IPAddress -notlike "169.254*" 
    } | Select-Object -ExpandProperty IPAddress)
} catch {}

Write-Host "`n  Primary LAN IP Address : " -NoNewline -ForegroundColor Gray
Write-Host "$lanIp" -ForegroundColor Yellow
Write-Host "  Localhost              : " -NoNewline -ForegroundColor Gray
Write-Host "127.0.0.1 / localhost" -ForegroundColor Yellow
if ($allIps.Count -gt 1) {
    Write-Host "  All Network Interfaces : " -NoNewline -ForegroundColor Gray
    Write-Host ($allIps -join ", ") -ForegroundColor DarkGray
}

# 3. Check Prerequisites
Write-Info "`nVerifying runtime toolchains..."
$tools = @(
    @{ Name = "dotnet"; Check = "dotnet --version" },
    @{ Name = "go";     Check = "go version" },
    @{ Name = "node";   Check = "node --version" },
    @{ Name = "npm";    Check = "npm --version" }
)

$missingTools = @()
foreach ($tool in $tools) {
    try {
        $ver = Invoke-Expression $tool.Check 2>$null
        if ($LASTEXITCODE -eq 0 -and $ver) {
            Write-Host "  [OK] $($tool.Name): " -NoNewline -ForegroundColor Green
            Write-Host ($ver.Trim() -split "`n")[0] -ForegroundColor Gray
        } else {
            $missingTools += $tool.Name
        }
    } catch {
        $missingTools += $tool.Name
    }
}

if ($missingTools.Count -gt 0) {
    Write-Err "Missing required tools: $($missingTools -join ', '). Please install them first."
    exit 1
}

# 4. Cleanup Previous Running Instances
$pidFile = Join-Path $RootPath ".services.json"
$logDir = Join-Path $RootPath "logs"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }

if (Test-Path $pidFile) {
    Write-Warn "Found active processes from previous run. Cleaning up..."
    try {
        $oldState = Get-Content $pidFile -Raw | ConvertFrom-Json
        foreach ($proc in $oldState.processes) {
            try {
                $p = Get-Process -Id $proc.pid -ErrorAction SilentlyContinue
                if ($p) {
                    Write-Host "  Stopping previous $($proc.name) (PID: $($proc.pid))..." -ForegroundColor DarkGray
                    Stop-Process -Id $proc.pid -Force -ErrorAction SilentlyContinue
                }
            } catch {}
        }
    } catch {}
    Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
}

# 5. Define Services Specification
$services = @(
    @{
        Name = "dotnet-api"
        Title = "ASP.NET Core REST API"
        Dir = Join-Path $RootPath "services/dotnet-api"
        Cmd = "dotnet"
        Args = "run --no-launch-profile --urls http://0.0.0.0:5229"
        Port = 5229
        HealthUrl = "http://localhost:5229/health"
        ReadyUrl = "http://localhost:5229/ready"
        Desc = "Core REST API, Auth, OpenAPI, Orders, Couriers"
    },
    @{
        Name = "dotnet-worker"
        Title = "ASP.NET Core Worker"
        Dir = Join-Path $RootPath "services/dotnet-worker"
        Cmd = "dotnet"
        Args = "run --no-launch-profile"
        Port = $null
        HealthUrl = $null
        ReadyUrl = $null
        Desc = "Outbox message publisher and Kafka consumer"
    },
    @{
        Name = "go-ingest"
        Title = "Go Location Ingest"
        Dir = Join-Path $RootPath "services/go-ingest"
        Cmd = "go"
        Args = "run main.go"
        Port = 8081
        HealthUrl = "http://localhost:8081/health"
        ReadyUrl = "http://localhost:8081/ready"
        Desc = "Real-time courier location ingest service"
    },
    @{
        Name = "go-dispatch"
        Title = "Go Dispatch Engine"
        Dir = Join-Path $RootPath "services/go-dispatch"
        Cmd = "go"
        Args = "run main.go"
        Port = 8082
        HealthUrl = "http://localhost:8082/health"
        ReadyUrl = "http://localhost:8082/ready"
        Desc = "Dispatch & assignment state machine engine"
    },
    @{
        Name = "go-gateway"
        Title = "Go Realtime Gateway"
        Dir = Join-Path $RootPath "services/go-gateway"
        Cmd = "go"
        Args = "run main.go"
        Port = 8083
        HealthUrl = "http://localhost:8083/health"
        ReadyUrl = "http://localhost:8083/ready"
        Desc = "WebSocket server and Redis Pub/Sub fan-out"
    },
    @{
        Name = "go-eta"
        Title = "Go ETA Service"
        Dir = Join-Path $RootPath "services/go-eta"
        Cmd = "go"
        Args = "run main.go"
        Port = 8084
        HealthUrl = "http://localhost:8084/health"
        ReadyUrl = "http://localhost:8084/ready"
        Desc = "Routing and travel duration estimator"
    }
)

if (-not $NoFrontend) {
    $services += @{
        Name = "angular-web"
        Title = "Angular Frontend"
        Dir = Join-Path $RootPath "web/angular"
        Cmd = "npm.cmd"
        Args = "start -- --host 0.0.0.0 --port 4200"
        Port = 4200
        HealthUrl = "http://localhost:4200"
        ReadyUrl = $null
        Desc = "Angular Web Console & Customer Tracking UI"
    }
}

# 6. Launch Services
Write-Header "LAUNCHING PLATFORM SERVICES"
$runningProcesses = @()

foreach ($svc in $services) {
    $svcLog = Join-Path $logDir "$($svc.Name).log"
    if (Test-Path $svcLog) { Remove-Item $svcLog -Force -ErrorAction SilentlyContinue }

    Write-Host "  Starting " -NoNewline -ForegroundColor White
    Write-Host "$($svc.Title) " -NoNewline -ForegroundColor Cyan
    if ($svc.Port) { Write-Host "(Port: $($svc.Port))..." -ForegroundColor DarkGray }
    else { Write-Host "(Background Worker)..." -ForegroundColor DarkGray }

    if ($Windowed) {
        $proc = Start-Process -FilePath "cmd.exe" `
            -ArgumentList "/k title $($svc.Title) && cd /d `"$($svc.Dir)`" && $($svc.Cmd) $($svc.Args)" `
            -WorkingDirectory $svc.Dir `
            -PassThru
    } else {
        $proc = Start-Process -FilePath "cmd.exe" `
            -ArgumentList "/c `"$($svc.Cmd) $($svc.Args) > `"$svcLog`" 2>&1`"" `
            -WorkingDirectory $svc.Dir `
            -WindowStyle Hidden `
            -PassThru
    }

    $runningProcesses += @{
        name = $svc.Name
        title = $svc.Title
        port = $svc.Port
        pid = $proc.Id
        healthUrl = $svc.HealthUrl
        desc = $svc.Desc
        process = $proc
    }
}

# Save PIDs for stop script
$jsonState = @{
    startedAt = (Get-Date).ToString("o")
    lanIp = $lanIp
    processes = $runningProcesses | ForEach-Object { @{ name = $_.name; pid = $_.pid; port = $_.port } }
} | ConvertTo-Json -Depth 3
Set-Content -Path $pidFile -Value $jsonState -Force

# Helper to test TCP socket connectivity quickly
function Test-TcpPort {
    param([string]$HostName, [int]$Port, [int]$TimeoutMs = 800)
    try {
        $tcp = New-Object System.Net.Sockets.TcpClient
        $iar = $tcp.BeginConnect($HostName, $Port, $null, $null)
        $wait = $iar.AsyncWaitHandle.WaitOne($TimeoutMs, $false)
        if ($wait -and $tcp.Connected) {
            $tcp.EndConnect($iar)
            $tcp.Close()
            return $true
        }
        $tcp.Close()
        return $false
    } catch {
        return $false
    }
}

# 7. Health Probe & Wait
Write-Header "WAITING FOR SERVICES TO REPORT HEALTHY"

foreach ($svc in $runningProcesses) {
    if (-not $svc.port -and -not $svc.healthUrl) { continue }
    
    $isUp = $false
    $svcWaitStart = [DateTime]::UtcNow
    Write-Host "  Checking $($svc.title)... " -NoNewline -ForegroundColor Gray

    while (([DateTime]::UtcNow - $svcWaitStart).TotalSeconds -lt $TimeoutSeconds) {
        if ($svc.port) {
            if (Test-TcpPort -HostName "127.0.0.1" -Port $svc.port) {
                $isUp = $true
                break
            }
        } elseif ($svc.healthUrl) {
            try {
                $resp = Invoke-WebRequest -Uri $svc.healthUrl -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
                if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 400) {
                    $isUp = $true
                    break
                }
            } catch {}
        }
        Start-Sleep -Milliseconds 500
    }

    if ($isUp) {
        Write-Host "HEALTHY" -ForegroundColor Green
    } else {
        Write-Host "STARTING UP (in progress)" -ForegroundColor Yellow
    }
}

# 8. Render Beautiful Service Dashboard
Write-Header "SYSTEM SERVICES CONNECTION & IP DASHBOARD"

Write-Host "  HOST LAN IP : " -NoNewline -ForegroundColor White
Write-Host "$lanIp" -ForegroundColor Yellow
Write-Host "  LOCALHOST   : " -NoNewline -ForegroundColor White
Write-Host "127.0.0.1" -ForegroundColor Yellow
Write-Host "--------------------------------------------------------------------------------" -ForegroundColor DarkCyan

Write-Host ("{0,-18} {1,-6} {2,-27} {3,-27}" -f "SERVICE", "PORT", "LOCAL URL", "NETWORK LAN URL") -ForegroundColor Cyan
Write-Host ("{0,-18} {1,-6} {2,-27} {3,-27}" -f "-------", "----", "---------", "---------------") -ForegroundColor DarkGray

foreach ($svc in $runningProcesses) {
    if ($svc.port) {
        $localUrl = "http://localhost:$($svc.port)"
        $networkUrl = "http://${lanIp}:$($svc.port)"
        Write-Host ("{0,-18} {1,-6} " -f $svc.name, $svc.port) -NoNewline -ForegroundColor White
        Write-Host ("{0,-27} " -f $localUrl) -NoNewline -ForegroundColor Green
        Write-Host ("{0,-27}" -f $networkUrl) -ForegroundColor Yellow
    } else {
        Write-Host ("{0,-18} {1,-6} " -f $svc.name, "BG") -NoNewline -ForegroundColor White
        Write-Host ("{0,-27} " -f "(Background Worker)") -NoNewline -ForegroundColor DarkGray
        Write-Host ("{0,-27}" -f "(N/A)") -ForegroundColor DarkGray
    }
}

Write-Host "--------------------------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host "  KEY PLATFORM ENDPOINTS:" -ForegroundColor White
Write-Host "  - Angular App     : " -NoNewline -ForegroundColor Gray
Write-Host "http://localhost:4200  |  http://${lanIp}:4200" -ForegroundColor Cyan
Write-Host "  - OpenAPI Docs    : " -NoNewline -ForegroundColor Gray
Write-Host "http://localhost:5229/openapi/v1.json" -ForegroundColor Cyan
Write-Host "  - Public JWKS     : " -NoNewline -ForegroundColor Gray
Write-Host "http://localhost:5229/.well-known/jwks.json" -ForegroundColor Cyan
Write-Host "  - Health Checks   : " -NoNewline -ForegroundColor Gray
Write-Host "http://localhost:5229/health (API) | :8081/health (Ingest)" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

Write-Host "`nAll services are running! " -ForegroundColor Green
Write-Host "Commands:" -ForegroundColor White
Write-Host "  - Press " -NoNewline -ForegroundColor Gray
Write-Host "[Q]" -NoNewline -ForegroundColor Yellow
Write-Host " to gracefully stop all services." -ForegroundColor Gray
Write-Host "  - Or run " -NoNewline -ForegroundColor Gray
Write-Host ".\stop-all.ps1" -NoNewline -ForegroundColor Yellow
Write-Host " from any other terminal window.`n" -ForegroundColor Gray

# 9. Interactive Monitor Loop
if ($Detach) {
    Write-Info "Services are running in detached background mode."
    Write-Info "Run .\stop-all.ps1 or stop-all.bat to stop all services at any time.`n"
    return
}

$canCheckKeys = $false
try {
    $canCheckKeys = -not [Console]::IsInputRedirected
} catch {
    $canCheckKeys = $false
}

try {
    while ($true) {
        if ($canCheckKeys) {
            try {
                if ([Console]::KeyAvailable) {
                    $key = [Console]::ReadKey($true)
                    if ($key.Key -eq [ConsoleKey]::Q) {
                        Write-Host "`nShutting down all services..." -ForegroundColor Yellow
                        break
                    }
                }
            } catch {}
        }

        # Check if any process exited unexpectedly
        foreach ($procInfo in $runningProcesses) {
            if ($procInfo.process.HasExited) {
                Write-Warn "$($procInfo.name) (PID: $($procInfo.pid)) stopped unexpectedly with exit code $($procInfo.process.ExitCode)."
            }
        }

        Start-Sleep -Seconds 1
    }
} finally {
    & (Join-Path $RootPath "stop-all.ps1")
}
