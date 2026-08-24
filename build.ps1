# build.ps1
# Clean, build, test and reinstall the tool locally.

$ErrorActionPreference = "Stop"

if (Get-Command "central-config" -ErrorAction SilentlyContinue) {
    Write-Host "Uninstalling the existing CentralConfigGenerator tool..." -ForegroundColor Cyan
    dotnet tool uninstall --global CentralConfigGenerator
}

Write-Host "Cleaning..." -ForegroundColor Cyan
dotnet clean

Write-Host "Restoring..." -ForegroundColor Cyan
dotnet restore

Write-Host "Building..." -ForegroundColor Cyan
dotnet build --no-restore --configuration Release

Write-Host "Testing..." -ForegroundColor Cyan
dotnet test --no-build --configuration Release

Write-Host "Packing..." -ForegroundColor Cyan
dotnet pack CentralConfigGenerator/CentralConfigGenerator.csproj --configuration Release --output ./nupkg

Write-Host "Installing the local build..." -ForegroundColor Cyan
dotnet tool install --global --add-source ./nupkg CentralConfigGenerator

Write-Host "Done. Try: central-config doctor" -ForegroundColor Green
