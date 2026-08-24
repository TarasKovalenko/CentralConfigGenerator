#!/usr/bin/env bash
# Clean, build, test and reinstall the tool locally.
set -euo pipefail

if command -v central-config >/dev/null 2>&1; then
  echo "Uninstalling the existing CentralConfigGenerator tool..."
  dotnet tool uninstall --global CentralConfigGenerator
fi

echo "Cleaning..."   && dotnet clean
echo "Restoring..."  && dotnet restore
echo "Building..."   && dotnet build --no-restore --configuration Release
echo "Testing..."    && dotnet test --no-build --configuration Release
echo "Packing..."    && dotnet pack CentralConfigGenerator/CentralConfigGenerator.csproj --configuration Release --output ./nupkg

echo "Installing the local build..."
dotnet tool install --global --add-source ./nupkg CentralConfigGenerator

echo "Done. Try: central-config doctor"
