# TaxReader - Start All Services
# Usage: Right-click -> Run with PowerShell, or: powershell -ExecutionPolicy Bypass -File start.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$logDir = Join-Path $env:TEMP "taxreader-logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$backendLog = Join-Path $logDir "backend.log"
$backendErrLog = Join-Path $logDir "backend.err.log"
$frontendLog = Join-Path $logDir "frontend.log"
$frontendErrLog = Join-Path $logDir "frontend.err.log"

function Get-PreferredLanIp {
    $preferredTypes = @(
        [System.Net.NetworkInformation.NetworkInterfaceType]::Ethernet,
        [System.Net.NetworkInformation.NetworkInterfaceType]::GigabitEthernet,
        [System.Net.NetworkInformation.NetworkInterfaceType]::FastEthernetT,
        [System.Net.NetworkInformation.NetworkInterfaceType]::FastEthernetFx,
        [System.Net.NetworkInformation.NetworkInterfaceType]::Wireless80211
    )

    $virtualInterfacePattern = "Hyper-V|WSL|Docker|VirtualBox|VMware|Tailscale|ZeroTier|Loopback|VPN"

    $networkInterfaces = [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() |
        Where-Object {
            $_.OperationalStatus -eq [System.Net.NetworkInformation.OperationalStatus]::Up -and
            $_.NetworkInterfaceType -in $preferredTypes -and
            $_.Name -notmatch $virtualInterfacePattern -and
            $_.Description -notmatch $virtualInterfacePattern
        }

    foreach ($networkInterface in $networkInterfaces) {
        $properties = $networkInterface.GetIPProperties()

        $hasIpv4Gateway = $properties.GatewayAddresses | Where-Object {
            $_.Address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork -and
            $_.Address.IPAddressToString -ne "0.0.0.0"
        }

        if (-not $hasIpv4Gateway) {
            continue
        }

        $ipv4Address = $properties.UnicastAddresses | Where-Object {
            $_.Address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork -and
            $_.Address.IPAddressToString -notlike "169.254.*"
        } | Select-Object -First 1

        if ($ipv4Address) {
            return $ipv4Address.Address.IPAddressToString
        }
    }

    $fallbackIp = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName()) |
        Where-Object {
            $_.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork -and
            $_.IPAddressToString -notlike "127.*" -and
            $_.IPAddressToString -notlike "169.254.*"
        } |
        Select-Object -First 1

    if ($fallbackIp) {
        return $fallbackIp.IPAddressToString
    }

    return $null
}

function Wait-HttpEndpoint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [int]$TimeoutSeconds = 45
    )

    $elapsed = 0
    while ($elapsed -lt $TimeoutSeconds) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                return $true
            }
        } catch {
            # not ready yet - keep polling
        }
        Start-Sleep -Seconds 1
        $elapsed++
    }
    return $false
}

function Show-LogTail {
    param([string]$Label, [string]$LogPath, [string]$ErrLogPath, [int]$Lines = 30)

    Write-Host "`n  --- $Label log tail ($LogPath) ---" -ForegroundColor Red
    if (Test-Path $LogPath) {
        Get-Content -Path $LogPath -Tail $Lines | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    }
    if ((Test-Path $ErrLogPath) -and (Get-Item $ErrLogPath).Length -gt 0) {
        Write-Host "  --- $Label stderr tail ---" -ForegroundColor Red
        Get-Content -Path $ErrLogPath -Tail $Lines | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    }
}

# Load .env into the process environment so the bare-metal backend connects
# with the SAME Postgres credentials docker-compose used to init the db
# container - avoids the "role does not exist" mismatch when .env overrides
# POSTGRES_USER/POSTGRES_PASSWORD away from the appsettings.Development.json
# placeholders.
$envFile = Join-Path $root ".env"
if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()
        if ($line -and -not $line.StartsWith("#") -and $line.Contains("=")) {
            $key, $value = $line.Split("=", 2)
            $key = $key.Trim()
            $value = $value.Trim().Trim('"').Trim("'")
            if (-not (Test-Path "Env:\$key")) {
                Set-Item -Path "Env:\$key" -Value $value
            }
        }
    }
}

Write-Host "=== TaxReader Startup ===" -ForegroundColor Cyan

$lanIp = Get-PreferredLanIp

# Explicit -f suppresses Compose's automatic docker-compose.override.yml
# merge, so it must be listed by hand when present (it publishes the db
# port to localhost for the bare-metal backend below).
$composeFiles = @("-f", "$root\docker-compose.yml")
$overrideFile = Join-Path $root "docker-compose.override.yml"
if (Test-Path $overrideFile) {
    $composeFiles += @("-f", $overrideFile)
}

# 1. Start Docker (PostgreSQL)
Write-Host "`n[1/3] Starting Docker containers (PostgreSQL)..." -ForegroundColor Yellow
docker compose @composeFiles up -d db
if ($LASTEXITCODE -ne 0) {
    Write-Host "  docker compose up failed - is Docker Desktop running?" -ForegroundColor Red
    exit 1
}

$dbContainerId = (docker compose @composeFiles ps -q db).Trim()
if (-not $dbContainerId) {
    Write-Host "  Could not resolve the db container - check 'docker compose ps'." -ForegroundColor Red
    exit 1
}

Write-Host "  Waiting for PostgreSQL to become healthy..." -ForegroundColor Gray
$retries = 0
$healthy = $false
do {
    Start-Sleep -Seconds 1
    $status = docker inspect --format "{{.State.Health.Status}}" $dbContainerId 2>$null
    $healthy = $status -eq "healthy"
    $retries++
} while (-not $healthy -and $retries -lt 30)

if ($healthy) {
    Write-Host "  PostgreSQL ready." -ForegroundColor Green
} else {
    Write-Host "  PostgreSQL did not become healthy in time (status: $status)." -ForegroundColor Red
    docker logs $dbContainerId --tail 30
    exit 1
}

# 2. Start Backend API
Write-Host "`n[2/3] Starting Backend API (port 5190)..." -ForegroundColor Yellow

$pgUser = if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { "postgres" }
$pgPassword = if ($env:POSTGRES_PASSWORD) { $env:POSTGRES_PASSWORD } else { "postgres" }
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=belegpilot;Username=$pgUser;Password=$pgPassword"

Remove-Item -Path $backendLog, $backendErrLog -ErrorAction SilentlyContinue
$backendProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run", "--project", "$root\Backend\src\TaxReader.Api", "--", "--urls", "http://0.0.0.0:5190" `
    -PassThru -NoNewWindow `
    -RedirectStandardOutput $backendLog -RedirectStandardError $backendErrLog
Write-Host "  Backend API starting (PID: $($backendProcess.Id)). Log: $backendLog" -ForegroundColor Green

$backendReady = Wait-HttpEndpoint -Url "http://localhost:5190/scalar/v1" -TimeoutSeconds 60
if ($backendReady) {
    Write-Host "  Backend API reachable." -ForegroundColor Green
} else {
    Write-Host "  Backend API is not responding on http://localhost:5190." -ForegroundColor Red
    Show-LogTail -Label "Backend" -LogPath $backendLog -ErrLogPath $backendErrLog
}

# 3. Start Frontend
Write-Host "`n[3/3] Starting Frontend (port 3000)..." -ForegroundColor Yellow
Remove-Item -Path $frontendLog, $frontendErrLog -ErrorAction SilentlyContinue
$frontendProcess = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "npm run dev" `
    -WorkingDirectory "$root\Frontend" -PassThru -NoNewWindow `
    -RedirectStandardOutput $frontendLog -RedirectStandardError $frontendErrLog
Write-Host "  Frontend starting (PID: $($frontendProcess.Id)). Log: $frontendLog" -ForegroundColor Green

$frontendReady = Wait-HttpEndpoint -Url "http://localhost:3000" -TimeoutSeconds 45
if ($frontendReady) {
    Write-Host "  Frontend reachable." -ForegroundColor Green
} else {
    Write-Host "  Frontend is not responding on http://localhost:3000." -ForegroundColor Red
    Show-LogTail -Label "Frontend" -LogPath $frontendLog -ErrLogPath $frontendErrLog
}

# Summary
Write-Host "`n=== Startup summary ===" -ForegroundColor Cyan
Write-Host "  Frontend:  http://localhost:3000" -ForegroundColor White
Write-Host "  Backend:   http://localhost:5190" -ForegroundColor White
Write-Host "  API Docs:  http://localhost:5190/scalar/v1" -ForegroundColor White
if ($lanIp) {
    Write-Host "  Frontend (LAN):  http://${lanIp}:3000" -ForegroundColor White
    Write-Host "  Backend (LAN):   http://${lanIp}:5190" -ForegroundColor White
    Write-Host "  API Docs (LAN):  http://${lanIp}:5190/scalar/v1" -ForegroundColor White
}
Write-Host "  Frontend status: $(if ($frontendReady) { 'reachable' } else { 'NOT RESPONDING - see log above' })" -ForegroundColor White
Write-Host "  Backend status:  $(if ($backendReady) { 'reachable' } else { 'NOT RESPONDING - see log above' })" -ForegroundColor White
Write-Host "`n  Live logs: Get-Content -Wait '$backendLog'  /  Get-Content -Wait '$frontendLog'" -ForegroundColor Gray
Write-Host "  Stop everything: .\stop.ps1" -ForegroundColor Gray

if (-not $backendReady -or -not $frontendReady) {
    exit 1
}
