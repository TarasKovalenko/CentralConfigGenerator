using System.Xml.Linq;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Xml;

namespace CentralConfigGenerator.Core.Migration;

/// <summary>
/// Surgical edits to a single project file: stripping inline versions for a migration, putting
/// them back for a revert, and removing properties that moved to Directory.Build.props.
/// </summary>
public static class ProjectRewriter
{
    /// <summary>
    /// Removes inline <c>Version</c> from every <c>PackageReference</c>.
    /// Returns <c>null</c> when nothing changed or the file could not be parsed.
    /// </summary>
    public static string? StripVersions(ProjectFile projectFile, out IReadOnlyList<string> touched)
    {
        touched = [];

        if (!MsBuildXml.TryParse(projectFile.Content, out var document, out _))
        {
            return null;
        }

        var changed = false;
        var packages = new List<string>();

        foreach (var element in MsBuildXml.Descendants(document, "PackageReference"))
        {
            var id = MsBuildXml.AttributeValue(element, "Include")
                ?? MsBuildXml.AttributeValue(element, "Update");

            var attribute = MsBuildXml.Attribute(element, "Version");
            if (attribute is not null)
            {
                attribute.Remove();
                changed = true;
                if (id is not null)
                {
                    packages.Add(id);
                }

                continue;
            }

            var child = MsBuildXml.Elements(element, "Version").FirstOrDefault();
            if (child is null)
            {
                continue;
            }

            MsBuildXml.RemoveWithWhitespace(child);
            CollapseIfEmpty(element);
            changed = true;
            if (id is not null)
            {
                packages.Add(id);
            }
        }

        touched = packages;
        return changed ? MsBuildXml.ToString(document) : null;
    }

    /// <summary>Removes the inline version of one specific package from a project.</summary>
    public static string? StripVersion(ProjectFile projectFile, string packageId)
    {
        if (!MsBuildXml.TryParse(projectFile.Content, out var document, out _))
        {
            return null;
        }

        var changed = false;

        foreach (var element in MsBuildXml.Descendants(document, "PackageReference"))
        {
            var id =
                MsBuildXml.AttributeValue(element, "Include")
                ?? MsBuildXml.AttributeValue(element, "Update");

            if (!string.Equals(id, packageId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var attribute = MsBuildXml.Attribute(element, "Version");
            if (attribute is not null)
            {
                attribute.Remove();
                changed = true;
                continue;
            }

            var child = MsBuildXml.Elements(element, "Version").FirstOrDefault();
            if (child is not null)
            {
                MsBuildXml.RemoveWithWhitespace(child);
                CollapseIfEmpty(element);
                changed = true;
            }
        }

        return changed ? MsBuildXml.ToString(document) : null;
    }

    /// <summary>
    /// Writes versions back onto every <c>PackageReference</c> that lacks one, using the supplied
    /// central version map. Used by the <c>revert</c> command.
    /// </summary>
    public static string? RestoreVersions(
        ProjectFile projectFile,
        IReadOnlyDictionary<string, string> centralVersions,
        out IReadOnlyList<string> unresolved
    )
    {
        var missing = new List<string>();
        unresolved = missing;

        if (!MsBuildXml.TryParse(projectFile.Content, out var document, out _))
        {
            return null;
        }

        var changed = false;

        foreach (var element in MsBuildXml.Descendants(document, "PackageReference"))
        {
            if (MsBuildXml.Attribute(element, "Version") is not null)
            {
                continue;
            }

            if (MsBuildXml.Elements(element, "Version").Any())
            {
                continue;
            }

            var id =
                MsBuildXml.AttributeValue(element, "Include")
                ?? MsBuildXml.AttributeValue(element, "Update");

            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!centralVersions.TryGetValue(id, out var version))
            {
                missing.Add(id);
                continue;
            }

            InsertVersionAfterInclude(element, version);
            changed = true;
        }

        return changed ? MsBuildXml.ToString(document) : null;
    }

    /// <summary>Removes the named properties from a project because they were hoisted upwards.</summary>
    public static string? RemoveProperties(
        ProjectFile projectFile,
        IReadOnlyCollection<string> propertyNames,
        IReadOnlyDictionary<string, string>? onlyWhenValueMatches = null
    )
    {
        if (propertyNames.Count == 0)
        {
            return null;
        }

        if (!MsBuildXml.TryParse(projectFile.Content, out var document, out _))
        {
            return null;
        }

        var names = new HashSet<string>(propertyNames, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var group in MsBuildXml.Descendants(document, "PropertyGroup").ToList())
        {
            // Conditioned property groups are project-specific; leave them alone.
            if (!string.IsNullOrWhiteSpace(MsBuildXml.AttributeValue(group, "Condition")))
            {
                continue;
            }

            foreach (var element in group.Elements().ToList())
            {
                if (!names.Contains(element.Name.LocalName))
                {
                    continue;
                }

                if (
                    onlyWhenValueMatches is not null
                    && onlyWhenValueMatches.TryGetValue(element.Name.LocalName, out var expected)
                    && !string.Equals(element.Value.Trim(), expected, StringComparison.Ordinal)
                )
                {
                    continue;
                }

                MsBuildXml.RemoveWithWhitespace(element);
                changed = true;
            }

            MsBuildXml.RemoveIfEmpty(group);
        }

        return changed ? MsBuildXml.ToString(document) : null;
    }

    /// <summary>Removes a duplicate <c>PackageReference</c> element for the given package id.</summary>
    public static string? RemoveDuplicateReference(ProjectFile projectFile, string packageId)
    {
        if (!MsBuildXml.TryParse(projectFile.Content, out var document, out _))
        {
            return null;
        }

        var matches = MsBuildXml
            .Descendants(document, "PackageReference")
            .Where(e =>
                string.Equals(
                    MsBuildXml.AttributeValue(e, "Include"),
                    packageId,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .ToList();

        if (matches.Count < 2)
        {
            return null;
        }

        foreach (var duplicate in matches.Skip(1))
        {
            var parent = duplicate.Parent;
            MsBuildXml.RemoveWithWhitespace(duplicate);
            MsBuildXml.RemoveIfEmpty(parent);
        }

        return MsBuildXml.ToString(document);
    }

    /// <summary>Rewrites a package id so its casing matches the canonical spelling from the feed.</summary>
    public static string? NormalizeCasing(ProjectFile projectFile, string canonicalId)
    {
        if (!MsBuildXml.TryParse(projectFile.Content, out var document, out _))
        {
            return null;
        }

        var changed = false;

        foreach (var element in MsBuildXml.Descendants(document, "PackageReference"))
        {
            var attribute = MsBuildXml.Attribute(element, "Include");
            if (
                attribute is null
                || !string.Equals(attribute.Value, canonicalId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(attribute.Value, canonicalId, StringComparison.Ordinal)
            )
            {
                continue;
            }

            attribute.Value = canonicalId;
            changed = true;
        }

        return changed ? MsBuildXml.ToString(document) : null;
    }

    /// <summary>
    /// Adds a Version attribute directly after Include. LINQ to XML has no attribute-insert API,
    /// so the attribute list is rebuilt in the desired order.
    /// </summary>
    private static void InsertVersionAfterInclude(XElement element, string version)
    {
        var attributes = element.Attributes().ToList();
        element.RemoveAttributes();

        var inserted = false;

        foreach (var attribute in attributes)
        {
            element.Add(new XAttribute(attribute.Name, attribute.Value));

            if (
                inserted
                || !string.Equals(attribute.Name.LocalName, "Include", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(attribute.Name.LocalName, "Update", StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            element.Add(new XAttribute("Version", version));
            inserted = true;
        }

        if (!inserted)
        {
            element.Add(new XAttribute("Version", version));
        }
    }

    private static void CollapseIfEmpty(XElement element)
    {
        if (element.Elements().Any())
        {
            return;
        }

        // Drop leftover whitespace so <PackageReference ...>\n  </PackageReference> becomes self-closing.
        foreach (var node in element.Nodes().OfType<XText>().ToList())
        {
            if (node.Value.Trim().Length == 0)
            {
                node.Remove();
            }
        }
    }
}
