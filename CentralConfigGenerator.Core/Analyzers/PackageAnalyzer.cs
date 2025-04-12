using System.Xml.Linq;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Analyzers;

public class PackageAnalyzer : IPackageAnalyzer
{
    public Dictionary<string, string> ExtractPackageVersions(IEnumerable<ProjectFile> projectFiles)
    {
        var packageVersions = new Dictionary<string, Version>();
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

                    // Store the original string version
                    stringVersions.TryAdd(packageName, versionStr);

                    // Try to parse as version for comparison
                    if (Version.TryParse(versionStr, out var version))
                    {
                        if (!packageVersions.ContainsKey(packageName) || version > packageVersions[packageName])
                        {
                            packageVersions[packageName] = version;
                            stringVersions[packageName] = versionStr;
                        }
                    }
                    // todo: Handle other version formats
                    // For version ranges or other formats, we can't easily compare
                    // so we'll just keep the first one we found
                }
            }
            catch (Exception)
            {
                // todo: Log error but continue with other projects
                continue;
            }
        }

        return stringVersions;
    }
}

public interface IPackageAnalyzer
{
    Dictionary<string, string> ExtractPackageVersions(IEnumerable<ProjectFile> projectFiles);
}