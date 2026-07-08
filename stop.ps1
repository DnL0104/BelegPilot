# TaxReader - Stop All Services
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== Stopping TaxReader ===" -ForegroundColor Cyan

# Stop frontend (node processes on port 3000)
$frontendPids = Get-NetTCPConnection -LocalPort 3000 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
foreach ($pid in $frontendPids) {
    Write-Host "  Stopping frontend (PID: $pid)..." -ForegroundColor Yellow
    Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
}

# Stop backend (dotnet processes on port 5190)
$backendPids = Get-NetTCPConnection -LocalPort 5190 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
foreach ($pid in $backendPids) {
    Write-Host "  Stopping backend (PID: $pid)..." -ForegroundColor Yellow
    Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
}

# Stop Docker containers
Write-Host "  Stopping Docker containers..." -ForegroundColor Yellow
docker compose -f "$PSScriptRoot\docker-compose.yml" stop db 2>$null

Write-Host "`n=== All services stopped ===" -ForegroundColor Green
