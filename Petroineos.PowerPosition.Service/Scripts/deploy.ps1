# deploy.ps1 - Complete build and deploy script
param(
    [string]$PublishPath = "C:\PetroineosService"
)

Write-Host "=== Building and Deploying Petroineos Service ===" -ForegroundColor Green

try {
    # Get the project directory (one level up from Scripts)
    $ScriptDir = $PSScriptRoot
    $ProjectDir = Join-Path $ScriptDir ".."
    $ProjectDir = [System.IO.Path]::GetFullPath($ProjectDir)
    
    Write-Host "Project directory: $ProjectDir" -ForegroundColor Cyan
    
    # Build and publish from project directory
    Write-Host "Building service..." -ForegroundColor Yellow
    cd $ProjectDir
    dotnet publish -c Release -o $PublishPath
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Build successful!" -ForegroundColor Green
    
    # Install service (go back to scripts directory)
    Write-Host "Installing service..." -ForegroundColor Yellow
    cd $ScriptDir
    .\install-service.ps1 -PublishPath $PublishPath
    
} catch {
    Write-Host "Deployment failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}