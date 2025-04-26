# CentralConfigGenerator

[![Made in Ukraine](https://img.shields.io/badge/made_in-ukraine-ffd700.svg?labelColor=0057b7)](https://taraskovalenko.github.io/)
[![build](https://github.com/TarasKovalenko/CentralConfigGenerator/actions/workflows/dotnet.yml/badge.svg)](https://github.com/TarasKovalenko/CentralConfigGenerator/actions)
[![CentralConfigGenerator NuGet current](https://img.shields.io/nuget/v/CentralConfigGenerator?label=CentralConfigGenerator)](https://www.nuget.org/packages/CentralConfigGenerator/)

## Goals
A modern .NET tool for automatically generating centralized configuration files for .NET projects. CentralConfig analyzes your solution structure and creates properly configured `Directory.Build.props` and `Directory.Packages.props` files to standardize settings across your projects.

## Terms of use

By using this project or its source code, for any purpose and in any shape or form, you grant your **implicit agreement** to all of the following statements:

- You unequivocally condemn Russia and its military aggression against Ukraine
- You recognize that Russia is an occupant that unlawfully invaded a sovereign state
- You agree that [Russia is a terrorist state](https://www.europarl.europa.eu/doceo/document/RC-9-2022-0482_EN.html)
- You fully support Ukraine's territorial integrity, including its claims over [temporarily occupied territories](https://en.wikipedia.org/wiki/Russian-occupied_territories_of_Ukraine)
- You reject false narratives perpetuated by Russian state propaganda

To learn more about the war and how you can help, [click here](https://war.ukraine.ua/). Glory to Ukraine! 🇺🇦

## Overview

CentralConfigGenerator helps you maintain consistent configuration across multiple .NET projects by:

1. Automatically generating `Directory.Build.props` files with common project properties
2. Automatically generating `Directory.Packages.props` files with centralized package versions
3. Updating your project files to use these centralized configurations

## Installation

```bash
dotnet tool install --global CentralConfigGenerator
```

## Usage

### Basic Commands

CentralConfigGenerator provides three main commands:

```bash
# Generate Directory.Build.props file with common project properties
central-config build [options]

# Generate Directory.Packages.props file for centralized package versions
central-config packages [options]

# Generate both files in one command
central-config all [options]
```

### Command Options

All commands support the following options:

- `-d, --directory <PATH>`: Specify the directory to scan (defaults to current directory)
- `-o, --overwrite`: Overwrite existing files (off by default)
- `-v, --verbose`: Enable verbose logging

### Examples

#### Generate Directory.Build.props

```bash
# Generate Directory.Build.props in the current directory
central-config build

# Generate in a specific directory and overwrite if exists
central-config build -d C:\Projects\MySolution -o
```

#### Generate Directory.Packages.props

```bash
# Generate Directory.Packages.props in the current directory
central-config packages

# Generate in a specific directory with verbose logging
central-config packages -d C:\Projects\MySolution -v
```

#### Generate Both Files

```bash
# Generate both files in one command
central-config all

# Generate both files with all options
central-config all -d C:\Projects\MySolution -o -v
```

## How It Works

### Directory.Build.props Generation

The `build` command:

1. Scans all `.csproj` files in the specified directory and subdirectories
2. Identifies common properties that appear in multiple projects
3. Extracts these properties into a `Directory.Build.props` file
4. Removes the extracted properties from individual project files

By default, CentralConfigGenerator will focus on the following key properties:
- `TargetFramework`
- `ImplicitUsings`
- `Nullable`

### Directory.Packages.props Generation

The `packages` command:

1. Scans all `.csproj` files in the specified directory and subdirectories
2. Extracts all package references and their versions
3. For each package, uses the highest version found across all projects
4. Generates a `Directory.Packages.props` file with these package versions
5. Removes version attributes from `PackageReference` elements in project files

### Understanding the Generated Files

#### Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

#### Directory.Packages.props

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="9.0.4" />
    <PackageVersion Include="Spectre.Console" Version="0.50.0" />
    <PackageVersion Include="Spectre.Console.Cli" Version="0.50.0" />
    <PackageVersion Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
  </ItemGroup>
</Project>
```

## Benefits

- **Consistent Configuration**: Ensure all projects use the same framework versions, language features, and code quality settings
- **Simplified Updates**: Update package versions or project settings in a single location
- **Reduced Duplication**: Remove redundant configuration from individual project files
- **Improved Maintainability**: Make your solution more maintainable by centralizing common settings

## Common Scenarios

### Migrating Existing Solutions

For existing solutions with many projects, use CentralConfigGenerator to centralize configuration:

```bash
# Navigate to the solution root
cd MySolution

# Generate both configuration files with verbose output
central-config all -v
```

## Limitations

- Projects with highly customized or conflicting settings may require manual adjustment after running CentralConfigGenerator
- For properties to be included in Directory.Build.props, they must appear with identical values in most projects
- Version conflicts in packages will be resolved by selecting the highest version found

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
