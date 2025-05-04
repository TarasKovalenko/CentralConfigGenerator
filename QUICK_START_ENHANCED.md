# Quick Start Guide: Enhanced Package Analysis

This guide will help you get started with the enhanced package analysis features of CentralConfigGenerator.

## Prerequisites

- .NET SDK 9.0 or later
- CentralConfigGenerator installed globally

## Basic Usage

### 1. Analyze Your Solution

Navigate to your solution directory and run:

```bash
central-config packages-enhanced -v
```

This will:
- Scan all project files
- Detect version conflicts
- Show a visual analysis report
- Ask for confirmation before making changes

### 2. Understanding the Output

You'll see a comprehensive report like this:

```
Package Analysis Summary
┌─────────────────────────┬─────────┐
│ Metric                  │  Value  │
├─────────────────────────┼─────────┤
│ Total Packages          │    15   │
│ Packages with Conflicts │    3    │
│ Warnings                │    4    │
└─────────────────────────┴─────────┘

Version Conflicts Detected:
┌─────────────────────┬──────────────────┬─────────────┬─────────────┐
│ Package             │ Project          │ Version     │ Type        │
├─────────────────────┼──────────────────┼─────────────┼─────────────┤
│ Newtonsoft.Json     │ Web.csproj       │ 11.0.2      │ Release     │
│ Newtonsoft.Json     │ Core.csproj      │ 13.0.3      │ Release     │
│                     │                  │             │             │
│ Serilog             │ Web.csproj       │ 2.10.0      │ Release     │
│ Serilog             │ Tests.csproj     │ 3.0.1       │ Release     │
└─────────────────────┴──────────────────┴─────────────┴─────────────┘
```

### 3. Conflict Resolution

The tool will automatically resolve conflicts using the highest version strategy. You'll be asked to confirm:

```
Version conflicts were detected. Continue with resolved versions? [y/n] (y): 
```

### 4. Review Changes

After confirmation, the tool will:
1. Create a `Directory.Packages.props` file
2. Remove version attributes from project files
3. Show a summary of changes

## Advanced Features

### Check Specific Directory

```bash
central-config packages-enhanced -d ./src/MyProjects -v
```

### Force Overwrite

```bash
central-config packages-enhanced --overwrite
```

### Dry Run (Coming Soon)

```bash
central-config packages-enhanced --dry-run
```

## Common Scenarios

### Scenario 1: Mixed Pre-release and Stable Versions

If you have both pre-release and stable versions:

```xml
<!-- Project1.csproj -->
<PackageReference Include="MyPackage" Version="1.0.0-beta" />

<!-- Project2.csproj -->
<PackageReference Include="MyPackage" Version="1.0.0" />
```

Result: The stable version `1.0.0` will be selected.

### Scenario 2: Version Ranges

When projects use version ranges:

```xml
<!-- Project1.csproj -->
<PackageReference Include="MyPackage" Version="[1.0.0,2.0.0)" />

<!-- Project2.csproj -->
<PackageReference Include="MyPackage" Version="1.5.0" />
```

Result: The range `[1.0.0,2.0.0)` will be preserved as it encompasses the specific version.

### Scenario 3: Outdated Packages

The tool will warn about significantly outdated packages:

```
Warnings:
┌─────────┬─────────────────────┬─────────────────────────────────────────────┐
│ Level   │ Package             │ Message                                      │
├─────────┼─────────────────────┼─────────────────────────────────────────────┤
│ Warning │ EntityFramework     │ This version is significantly outdated       │
└─────────┴─────────────────────┴─────────────────────────────────────────────┘
```

## Troubleshooting

### Issue: Variable Versions

If you have versions like `$(MyVersion)`:

```xml
<PackageReference Include="MyPackage" Version="$(MyVersion)" />
```

These will be preserved as-is in the centralized configuration.

### Issue: Conflicting Pre-release Versions

When multiple pre-release versions conflict:

```
MyPackage 1.0.0-alpha.1
MyPackage 1.0.0-beta.1
```

The tool correctly identifies that `beta.1` is newer than `alpha.1`.

## Best Practices

1. **Review Before Confirming**: Always review the conflict resolution before accepting
2. **Test After Migration**: Run your tests after centralizing package versions
3. **Use Stable Versions**: Prefer stable versions over pre-release in production
4. **Regular Updates**: Run the analysis periodically to keep versions consistent

## Next Steps

- Learn about [version management details](VERSION_MANAGEMENT.md)
- Read the [full documentation](README.md)

## Getting Help

If you encounter issues:

1. Run with verbose logging: `central-config packages-enhanced -v`
2. Open an issue on [GitHub](https://github.com/TarasKovalenko/CentralConfigGenerator/issues)
