using System.Xml.Linq;
using CentralConfigGenerator.Core.Models;
using NuGet.Versioning;
using Spectre.Console;

namespace CentralConfigGenerator.Core.Analyzers;

public class PackageAnalyzer : IPackageAnalyzer
{
    public Dictionary<string, string> ExtractPackageVersions(IEnumerable<ProjectFile> projectFiles)
    {
        var packageVersions = new Dictionary<string, NuGetVersion>();
        var stringVersions = new Dictionary<string, string>();

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

                    // Skip if no package name or version
                    if (string.IsNullOrWhiteSpace(packageName) || versionAttr == null)
                    {
                        continue;
                    }

                    var versionStr = versionAttr.Value;

                    // Handle version ranges and variables
                    if (VersionRange.TryParse(versionStr, out var versionRange) && versionRange.HasLowerBound)
                    {
                        var nugetVersion = versionRange.MinVersion;
                        
                        if (!packageVersions.ContainsKey(packageName) || nugetVersion > packageVersions[packageName])
                        {
                            packageVersions[packageName] = nugetVersion;
                            stringVersions[packageName] = versionStr;
                        }
                    }
                    else if (NuGetVersion.TryParse(versionStr, out var nugetVersion))
                    {
                        if (!packageVersions.ContainsKey(packageName) || nugetVersion > packageVersions[packageName])
                        {
                            packageVersions[packageName] = nugetVersion;
                            stringVersions[packageName] = versionStr;
                        }
                    }
                    else
                    {
                        // For non-parseable versions (like variables), just store them
                        stringVersions.TryAdd(packageName, versionStr);
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error parsing project file: {projectFile.Path}";
                AnsiConsole.MarkupLineInterpolated($"[red]Error:{errorMessage}[/]");
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            }
        }

        return stringVersions;
    }
}
