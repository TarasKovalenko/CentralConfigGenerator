using System.Xml.Linq;
using CentralConfigGenerator.Core.Analyzers.Abstractions;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Services;
using CentralConfigGenerator.Core.Services.Abstractions;
using NuGet.Versioning;

namespace CentralConfigGenerator.Core.Analyzers;

public class PackageAnalysisResult
{
    public Dictionary<string, string> ResolvedVersions { get; set; } = new();
    public Dictionary<string, List<VersionConflict>> Conflicts { get; set; } = new();
    public List<VersionWarning> Warnings { get; set; } = new();
}

public class VersionConflict
{
    public string ProjectFile { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsPreRelease { get; set; }
    public bool IsRange { get; set; }
}

public class VersionWarning
{
    public string PackageName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public WarningLevel Level { get; set; }
}

public enum WarningLevel
{
    Info,
    Warning,
    Error
}

public class EnhancedPackageAnalyzer(IVersionConflictResolver conflictResolver) : IEnhancedPackageAnalyzer
{
    public PackageAnalysisResult AnalyzePackages(IEnumerable<ProjectFile> projectFiles)
    {
        var result = new PackageAnalysisResult();
        var packageVersionsByPackage = new Dictionary<string, List<(string ProjectPath, string Version)>>();

        // Collect all versions
        foreach (var projectFile in projectFiles)
        {
            try
            {
                var xDoc = XDocument.Parse(projectFile.Content);
                var packageReferences = xDoc.Descendants("PackageReference");

                foreach (var packageRef in packageReferences)
                {
                    var packageName = packageRef.Attribute("Include")?.Value;
                    var versionAttr = packageRef.Attribute("Version");

                    if (string.IsNullOrWhiteSpace(packageName) || versionAttr == null)
                        continue;

                    var version = versionAttr.Value;
                    
                    if (!packageVersionsByPackage.ContainsKey(packageName))
                        packageVersionsByPackage[packageName] = new List<(string, string)>();
                    
                    packageVersionsByPackage[packageName].Add((projectFile.Path, version));
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add(new VersionWarning
                {
                    PackageName = projectFile.Path,
                    Message = $"Failed to parse project file: {ex.Message}",
                    Level = WarningLevel.Error
                });
            }
        }

        // Resolve conflicts and create final versions
        foreach (var package in packageVersionsByPackage)
        {
            var packageName = package.Key;
            var versions = package.Value;
            var uniqueVersions = versions.Select(v => v.Version).Distinct().ToList();

            if (uniqueVersions.Count == 1)
            {
                // No conflict
                result.ResolvedVersions[packageName] = uniqueVersions[0];
            }
            else
            {
                // Conflict detected
                result.Conflicts[packageName] = versions
                    .Select(v => CreateVersionConflict(v.ProjectPath, v.Version))
                    .ToList();

                // Try to resolve with the highest version strategy
                try
                {
                    var resolvedVersion = conflictResolver.Resolve(
                        packageName, 
                        uniqueVersions, 
                        VersionResolutionStrategy.Highest);
                    
                    result.ResolvedVersions[packageName] = resolvedVersion;
                    
                    result.Warnings.Add(new VersionWarning
                    {
                        PackageName = packageName,
                        Message = $"Multiple versions found. Resolved to: {resolvedVersion}",
                        Level = WarningLevel.Warning
                    });
                }
                catch (Exception ex)
                {
                    result.Warnings.Add(new VersionWarning
                    {
                        PackageName = packageName,
                        Message = $"Failed to resolve version conflict: {ex.Message}",
                        Level = WarningLevel.Error
                    });
                    
                    // Fallback: use most recent version
                    result.ResolvedVersions[packageName] = uniqueVersions.OrderDescending().First();
                }
            }

            // Check for pre-release usage
            CheckForPrereleaseUsage(packageName, result.ResolvedVersions[packageName], result.Warnings);
        }

        return result;
    }

    private static VersionConflict CreateVersionConflict(string projectPath, string version)
    {
        var conflict = new VersionConflict
        {
            ProjectFile = projectPath,
            Version = version
        };

        if (NuGetVersion.TryParse(version, out var nugetVersion))
        {
            conflict.IsPreRelease = nugetVersion.IsPrerelease;
        }
        else if (VersionRange.TryParse(version, out _))
        {
            conflict.IsRange = true;
        }

        return conflict;
    }

    private static void CheckForPrereleaseUsage(string packageName, string version, List<VersionWarning> warnings)
    {
        if (NuGetVersion.TryParse(version, out var nugetVersion) && nugetVersion.IsPrerelease)
        {
            warnings.Add(new VersionWarning
            {
                PackageName = packageName,
                Message = $"Using pre-release version: {version}",
                Level = WarningLevel.Info
            });
        }
    }
}
