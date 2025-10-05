# install-service.ps1
param(
    [string]$ServiceName = "Petroineos Power Position Service",
    [string]$DisplayName = "Petroineos Power Position Service", 
    [string]$Description = "Generates intra-day power position reports for traders",
    [string]$PublishPath = "C:\PetroineosService",
    [string]$Username,
    [string]$Password
)

$BinaryPath = Join-Path $PublishPath "Petroineos.PowerPosition.Service.exe"

Write-Host "=== Petroineos Power Position Service Installer ===" -ForegroundColor Green

# Check if binary exists
if (-not (Test-Path $BinaryPath)) {
    Write-Host "ERROR: Binary not found at: $BinaryPath" -ForegroundColor Red
    Write-Host "Please build and publish the service first using:" -ForegroundColor Yellow
    Write-Host "dotnet publish -c Release -o `"$PublishPath`"" -ForegroundColor Yellow
    exit 1
}

Write-Host "Installing Windows Service: $ServiceName"
Write-Host "Binary Path: $BinaryPath"

try {
    # Stop and remove existing service
    if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
        Write-Host "Service already exists. Stopping and removing..."
        Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
        sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 2
        Write-Host "Existing service removed." -ForegroundColor Green
    }

    # Install the service
    if ($Username -and $Password) {
        Write-Host "Installing with specified credentials..."
        $securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
        $credential = New-Object System.Management.Automation.PSCredential($Username, $securePassword)
        
        New-Service -Name $ServiceName `
                    -DisplayName $DisplayName `
                    -Description $Description `
                    -BinaryPathName $BinaryPath `
                    -Credential $credential `
                    -StartupType Automatic
    } else {
        Write-Host "Installing with LocalSystem account..."
        New-Service -Name $ServiceName `
                    -DisplayName $DisplayName `
                    -Description $Description `
                    -BinaryPathName $BinaryPath `
                    -StartupType Automatic
    }

    Write-Host "Service installed successfully!" -ForegroundColor Green
    
    # Start the service
    Write-Host "Starting service..."
    Start-Service $ServiceName
    Start-Sleep -Seconds 2
    
    # Check service status
    $service = Get-Service $ServiceName
    if ($service.Status -eq 'Running') {
        Write-Host "Service started successfully! Status: $($service.Status)" -ForegroundColor Green
    } else {
        Write-Host "Service installed but not running. Status: $($service.Status)" -ForegroundColor Yellow
    }
    
    Write-Host "`nService Information:" -ForegroundColor Cyan
    Write-Host "  Name: $ServiceName"
    Write-Host "  Display Name: $DisplayName" 
    Write-Host "  Binary: $BinaryPath"
    Write-Host "  Status: $($service.Status)"
    
} catch {
    Write-Host "ERROR: Failed to install service: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}