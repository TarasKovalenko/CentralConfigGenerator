# build.ps1
# Script to clean and build the solution

# remove previously installed tool
if (Get-Command "central-config" -ErrorAction SilentlyContinue) {
    Write-Host "Uninstalling existing CentralConfigGenerator tool..." -ForegroundColor Cyan
    dotnet tool uninstall --global CentralConfigGenerator
}

Write-Host "Cleaning solution..." -ForegroundColor Cyan
dotnet clean

Write-Host "Restoring packages..." -ForegroundColor Cyan
dotnet restore

Write-Host "Building solution..." -ForegroundColor Cyan
dotnet build --no-restore -c Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build completed successfully!" -ForegroundColor Green
}
else {
    Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Packing NuGet package..." -ForegroundColor Cyan
dotnet pack --no-build

if ($LASTEXITCODE -eq 0) {
    Write-Host "Package created successfully!" -ForegroundColor Green
    
    Write-Host "Publishing NuGet package..." -ForegroundColor Cyan
    dotnet tool install --global --add-source .\CentralConfigGenerator\nupkg CentralConfigGenerator
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Package published successfully!" -ForegroundColor Green
    }
    else {
        Write-Host "Package publishing failed with exit code $LASTEXITCODE" -ForegroundColor Red
    }
}
else {
    Write-Host "Package creation failed with exit code $LASTEXITCODE" -ForegroundColor Red
}