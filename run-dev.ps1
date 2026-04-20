$ErrorActionPreference = "Stop"

$port = 4883

try {
    $listener = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1

    if ($null -ne $listener) {
        Write-Host "Port $port is in use by PID $($listener.OwningProcess). Stopping it..." -ForegroundColor Yellow
        Stop-Process -Id $listener.OwningProcess -Force
        Write-Host "Freed port $port." -ForegroundColor Green
    }
}
catch {
    Write-Host "Could not check/stop existing listener on port ${port}: $($_.Exception.Message)" -ForegroundColor Red
    throw
}

Write-Host "Starting app on port $port..." -ForegroundColor Cyan
dotnet run
