# Advanced Version Management with NuGet.Versioning

This document explains how CentralConfigGenerator uses NuGet.Versioning to provide advanced version management capabilities.

## Overview

CentralConfigGenerator uses the `NuGet.Versioning` package to provide sophisticated version handling that goes beyond basic .NET version comparison. This integration enables proper semantic versioning support, version range handling, and accurate package version conflict resolution.

## Key Features

### 1. Semantic Versioning Support

The tool properly handles semantic versioning (SemVer) including:

- Major.Minor.Patch versions (e.g., `1.2.3`)
- Pre-release tags (e.g., `1.0.0-beta.1`, `2.0.0-rc.2`)
- Build metadata (e.g., `1.0.0+build.123`)

#### Example:
```xml
<!-- Pre-release versions are correctly compared -->
<PackageReference Include="MyPackage" Version="1.0.0-alpha.1" />
<PackageReference Include="MyPackage" Version="1.0.0-beta.1" />
<!-- Result: 1.0.0-beta.1 is selected as it's newer than alpha -->
```

### 2. Version Range Handling

Supports NuGet's version range syntax:

- Exact version: `1.0.0`
- Minimum version: `1.0.0`
- Exact range: `[1.0.0]`
- Minimum inclusive: `[1.0.0,)`
- Maximum inclusive: `(,1.0.0]`
- Range: `[1.0.0,2.0.0)`
- Floating version: `1.0.*`

#### Example:
```xml
<!-- Version ranges are preserved and understood -->
<PackageReference Include="MyPackage" Version="[1.0.0,2.0.0)" />
<PackageReference Include="MyPackage" Version="1.5.0" />
<!-- Result: [1.0.0,2.0.0) is selected as it encompasses the specific version -->
```

### 3. Conflict Resolution Strategies

The enhanced package analyzer provides multiple strategies for resolving version conflicts:

1. **Highest Version (Default)**: Selects the highest version among all referenced versions
2. **Lowest Version**: Selects the lowest version (useful for maximum compatibility)
3. **Most Common**: Selects the version used by most projects
4. **Manual**: Prompts for manual intervention when automatic resolution isn't suitable

#### Example:
```csharp
// Using the version conflict resolver
var resolver = new VersionConflictResolver();
var resolvedVersion = resolver.Resolve(
    "Newtonsoft.Json", 
    new[] { "11.0.2", "12.0.3", "13.0.1" }, 
    VersionResolutionStrategy.Highest
);
// Result: "13.0.1"
```

### 4. Compatibility Checking

The tool can check for known issues with specific package versions:

- Security vulnerabilities
- Performance regressions
- Breaking changes
- Deprecated versions

#### Example output:
```
Warnings:
┌─────────┬─────────────────────┬─────────────────────────────────────────────┐
│ Level   │ Package             │ Message                                      │
├─────────┼─────────────────────┼─────────────────────────────────────────────┤
│ Warning │ Newtonsoft.Json     │ Known security vulnerability in version 9.x  │
│ Info    │ System.Text.Json    │ Pre-release version detected                 │
│ Warning │ EntityFramework     │ This version is significantly outdated       │
└─────────┴─────────────────────┴─────────────────────────────────────────────┘
```

## Implementation Details

### Version Parsing

The tool uses `NuGetVersion.TryParse()` instead of the basic `Version.TryParse()`:

```csharp
// Old approach (limited)
if (Version.TryParse(versionStr, out var version))
{
    // Only handles numeric versions like "1.0.0"
}

// New approach (comprehensive)
if (NuGetVersion.TryParse(versionStr, out var nugetVersion))
{
    // Handles "1.0.0-beta.1+build.123" and more
}
```

### Version Comparison

NuGet.Versioning provides accurate version comparison following SemVer rules:

```csharp
var v1 = NuGetVersion.Parse("1.0.0-alpha");
var v2 = NuGetVersion.Parse("1.0.0-beta");
var v3 = NuGetVersion.Parse("1.0.0");

// Correct ordering: alpha < beta < release
Console.WriteLine(v1 < v2); // True
Console.WriteLine(v2 < v3); // True
```

### Handling Special Cases

The tool handles various special version formats:

1. **Variables**: `$(VersionPrefix)$(VersionSuffix)`
2. **Properties**: `$(MyPackageVersion)`
3. **Wildcards**: `1.0.*`
4. **Floating**: `1.*`

These are preserved in the output when they cannot be resolved to specific versions.

## Visual Reporting

The enhanced package analyzer provides detailed visual reports using Spectre.Console:

```
Package Analysis Summary
┌─────────────────────────┬─────────┐
│ Metric                  │  Value  │
├─────────────────────────┼─────────┤
│ Total Packages          │    25   │
│ Packages with Conflicts │    5    │
│ Warnings                │    8    │
└─────────────────────────┴─────────┘

Resolved Package Versions:
┌─────────────────────────────┬─────────────────┐
│ Package                     │ Version         │
├─────────────────────────────┼─────────────────┤
│ Microsoft.Extensions.Logging│ 8.0.0           │
│ Newtonsoft.Json            │ 13.0.3          │
│ NuGet.Versioning           │ 6.7.0           │
└─────────────────────────────┴─────────────────┘
```

## Best Practices

1. **Use Semantic Versioning**: Follow SemVer conventions for your package versions
2. **Avoid Floating Versions**: Use specific versions in production code
3. **Review Conflicts**: Always review detected conflicts before accepting resolutions
4. **Check Compatibility**: Pay attention to compatibility warnings
5. **Test After Migration**: Test your solution after centralizing package versions

## Troubleshooting

### Common Issues

1. **Unparseable Versions**: Some version formats (like variables) cannot be parsed
   - Solution: These are preserved as-is in the output

2. **Complex Version Ranges**: Overlapping ranges might cause unexpected resolutions
   - Solution: Review the resolution and adjust manually if needed

3. **Pre-release Dependencies**: Pre-release packages in production code
   - Solution: The tool warns about these; consider using stable versions

### Debug Information

Run with verbose logging to see detailed version analysis:

```bash
central-config packages-enhanced -v
```

This will show:
- Each version comparison
- Resolution decisions
- Parsing failures
- Compatibility check results

## API Reference

### Key Classes

1. `EnhancedPackageAnalyzer`: Main analyzer with conflict detection
2. `VersionConflictResolver`: Resolves version conflicts using various strategies
3. `VersionCompatibilityChecker`: Checks for known version issues
4. `VersionConflictVisualizer`: Creates visual reports of analysis results

### Extension Points

The system is designed to be extensible:

```csharp
// Custom resolution strategy
public class CustomVersionResolver : IVersionConflictResolver
{
    public string Resolve(string packageName, IEnumerable<string> versions, 
                         VersionResolutionStrategy strategy)
    {
        // Custom resolution logic
    }
}

// Custom compatibility checker
public class CustomCompatibilityChecker : IVersionCompatibilityChecker
{
    public Task<CompatibilityCheckResult> CheckCompatibilityAsync(
        string packageId, string version)
    {
        // Custom compatibility checks
    }
}
```

## Future Enhancements

Planned improvements include:

1. NuGet API integration for real-time version information
2. Custom resolution rules configuration
3. Integration with vulnerability databases
4. Support for dependency graph analysis
5. Automated upgrade path suggestions
