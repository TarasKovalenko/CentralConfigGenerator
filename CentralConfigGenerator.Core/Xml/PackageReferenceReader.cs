using System.Xml.Linq;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Xml;

/// <summary>
/// Extracts package references from project XML, covering attribute versions, child-element
/// versions, <c>VersionOverride</c> and <c>GlobalPackageReference</c>.
/// </summary>
public static class PackageReferenceReader
{
    public static IReadOnlyList<PackageReferenceInfo> Read(ProjectFile projectFile)
    {
        if (!MsBuildXml.TryParse(projectFile.Content, out var document, out _))
        {
            return [];
        }

        return Read(document, projectFile.Path);
    }

    public static IReadOnlyList<PackageReferenceInfo> Read(XDocument document, string projectPath)
    {
        var results = new List<PackageReferenceInfo>();

        foreach (var element in MsBuildXml.Descendants(document, "PackageReference"))
        {
            var info = ReadOne(element, projectPath, isGlobal: false);
            if (info is not null)
            {
                results.Add(info);
            }
        }

        foreach (var element in MsBuildXml.Descendants(document, "GlobalPackageReference"))
        {
            var info = ReadOne(element, projectPath, isGlobal: true);
            if (info is not null)
            {
                results.Add(info);
            }
        }

        return results;
    }

    /// <summary>Reads <c>PackageVersion</c> entries from a Directory.Packages.props document.</summary>
    public static IReadOnlyDictionary<string, string> ReadPackageVersions(XDocument document)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in MsBuildXml.Descendants(document, "PackageVersion"))
        {
            var id = MsBuildXml.AttributeValue(element, "Include")
                ?? MsBuildXml.AttributeValue(element, "Update");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var version =
                MsBuildXml.AttributeValue(element, "Version")
                ?? MsBuildXml.Elements(element, "Version").FirstOrDefault()?.Value;

            if (!string.IsNullOrWhiteSpace(version))
            {
                results[id.Trim()] = version.Trim();
            }
        }

        return results;
    }

    private static PackageReferenceInfo? ReadOne(XElement element, string projectPath, bool isGlobal)
    {
        var id =
            MsBuildXml.AttributeValue(element, "Include")
            ?? MsBuildXml.AttributeValue(element, "Update");

        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var versionAttribute = MsBuildXml.Attribute(element, "Version");
        var versionElement = MsBuildXml.Elements(element, "Version").FirstOrDefault();
        var overrideAttribute = MsBuildXml.Attribute(element, "VersionOverride");

        var version =
            versionAttribute?.Value ?? versionElement?.Value ?? overrideAttribute?.Value;

        var privateAssets =
            MsBuildXml.AttributeValue(element, "PrivateAssets")
            ?? MsBuildXml.Elements(element, "PrivateAssets").FirstOrDefault()?.Value;

        return new PackageReferenceInfo
        {
            PackageId = id.Trim(),
            Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim(),
            ProjectPath = projectPath,
            Condition = MsBuildXml.EffectiveCondition(element),
            VersionIsElement = versionAttribute is null && versionElement is not null,
            IsGlobal = isGlobal,
            IsPrivateAssets = !string.IsNullOrWhiteSpace(privateAssets),
        };
    }
}
