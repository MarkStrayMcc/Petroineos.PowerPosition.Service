# uninstall-service.ps1
param(
    [string]$ServiceName = "Petroineos Power Position Service"
)

Write-Host "Uninstalling service: $ServiceName" -ForegroundColor Yellow

try {
    if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
        Write-Host "Stopping service..."
        Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        
        Write-Host "Removing service..."
        sc.exe delete $ServiceName
        Start-Sleep -Seconds 2
        
        Write-Host "Service uninstalled successfully!" -ForegroundColor Green
    } else {
        Write-Host "Service not found." -ForegroundColor Yellow
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}