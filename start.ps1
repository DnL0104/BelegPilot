# TaxReader - Start All Services
# Usage: Right-click -> Run with PowerShell, or: powershell -ExecutionPolicy Bypass -File start.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

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

function Test-HttpEndpoint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,

        [int]$TimeoutSeconds = 5
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSeconds
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 500
    }
    catch {
        return $false
    }
}

Write-Host "=== TaxReader Startup ===" -ForegroundColor Cyan

$lanIp = Get-PreferredLanIp

# 1. Start Docker (PostgreSQL)
Write-Host "`n[1/3] Starting Docker containers (PostgreSQL)..." -ForegroundColor Yellow
Start-Process -FilePath "docker" -ArgumentList "compose", "-f", "$root\docker-compose.yml", "up", "-d", "db" -NoNewWindow -Wait
Write-Host "  PostgreSQL is running." -ForegroundColor Green

# 2. Wait for PostgreSQL to be ready
Write-Host "  Waiting for PostgreSQL to accept connections..." -ForegroundColor Gray
$retries = 0
do {
    $retries++
    Start-Sleep -Seconds 1
    $ready = docker exec belegpilot-db pg_isready 2>$null
} while ($LASTEXITCODE -ne 0 -and $retries -lt 15)

if ($retries -ge 15) {
    Write-Host "  Warning: PostgreSQL may not be ready yet." -ForegroundColor Red
} else {
    Write-Host "  PostgreSQL ready." -ForegroundColor Green
}

# 3. Start Backend API
Write-Host "`n[2/3] Starting Backend API (port 5190)..." -ForegroundColor Yellow
$backendJob = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "$root\Backend\src\TaxReader.Api", "--", "--urls", "http://0.0.0.0:5190" -PassThru -WindowStyle Normal
Write-Host "  Backend API starting (PID: $($backendJob.Id))." -ForegroundColor Green

# Give the backend a moment to start
Start-Sleep -Seconds 6

$backendReady = Test-HttpEndpoint -Url "http://localhost:5190/scalar/v1"
if ($backendReady) {
    Write-Host "  Backend API reachable." -ForegroundColor Green
} else {
    Write-Host "  Warning: Backend API is not responding on http://localhost:5190." -ForegroundColor Red
    Write-Host "  Check the backend window for build or startup errors." -ForegroundColor Yellow
}

# 4. Start Frontend
Write-Host "`n[3/3] Starting Frontend (port 3000)..." -ForegroundColor Yellow
$frontendJob = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "npm run dev" -WorkingDirectory "$root\Frontend" -PassThru -WindowStyle Normal
Write-Host "  Frontend starting (PID: $($frontendJob.Id))." -ForegroundColor Green

Start-Sleep -Seconds 6

$frontendReady = Test-HttpEndpoint -Url "http://localhost:3000"
if ($frontendReady) {
    Write-Host "  Frontend reachable." -ForegroundColor Green
} else {
    Write-Host "  Warning: Frontend is not responding on http://localhost:3000." -ForegroundColor Red
    Write-Host "  Check the frontend window for startup errors." -ForegroundColor Yellow
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
Write-Host "  Frontend status: $(if ($frontendReady) { 'reachable' } else { 'not responding' })" -ForegroundColor White
Write-Host "  Backend status:  $(if ($backendReady) { 'reachable' } else { 'not responding' })" -ForegroundColor White
Write-Host "`nPress Ctrl+C in each window to stop, or run: .\stop.ps1" -ForegroundColor Gray
