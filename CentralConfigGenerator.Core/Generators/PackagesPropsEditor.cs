using System.Xml.Linq;
using CentralConfigGenerator.Core.Xml;

namespace CentralConfigGenerator.Core.Generators;

/// <summary>Targeted edits to an existing Directory.Packages.props file.</summary>
public static class PackagesPropsEditor
{
    /// <summary>Sets (or adds) the version for one package. Returns null when nothing changed.</summary>
    public static string? SetVersion(string content, string packageId, string version, string indent = "  ")
    {
        if (!MsBuildXml.TryParse(content, out var document, out _) || document.Root is null)
        {
            return null;
        }

        var element = FindEntry(document, packageId);

        if (element is not null)
        {
            var attribute = MsBuildXml.Attribute(element, "Version");
            if (attribute is not null)
            {
                if (string.Equals(attribute.Value, version, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                attribute.Value = version;
                return MsBuildXml.ToString(document);
            }

            var child = MsBuildXml.Elements(element, "Version").FirstOrDefault();
            if (child is not null)
            {
                if (string.Equals(child.Value.Trim(), version, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                child.Value = version;
            }
            else
            {
                element.SetAttributeValue("Version", version);
            }

            return MsBuildXml.ToString(document);
        }

        var root = document.Root;
        var ns = root.Name.Namespace;

        var itemGroup =
            MsBuildXml
                .Elements(root, "ItemGroup")
                .LastOrDefault(g => MsBuildXml.Elements(g, "PackageVersion").Any());

        if (itemGroup is null)
        {
            itemGroup = new XElement(ns + "ItemGroup");
            root.Add(new XText("\n" + indent), itemGroup, new XText("\n"));
        }

        itemGroup.Add(
            new XText("\n" + indent + indent),
            new XElement(
                ns + "PackageVersion",
                new XAttribute("Include", packageId),
                new XAttribute("Version", version)
            )
        );

        if (itemGroup.LastNode is XElement)
        {
            itemGroup.Add(new XText("\n" + indent));
        }

        return MsBuildXml.ToString(document);
    }

    /// <summary>Removes a package's central version entry.</summary>
    public static string? RemoveEntry(string content, string packageId)
    {
        if (!MsBuildXml.TryParse(content, out var document, out _))
        {
            return null;
        }

        var element = FindEntry(document, packageId);
        if (element is null)
        {
            return null;
        }

        var parent = element.Parent;
        MsBuildXml.RemoveWithWhitespace(element);
        MsBuildXml.RemoveIfEmpty(parent);

        return MsBuildXml.ToString(document);
    }

    /// <summary>Rewrites a package id so its casing matches the canonical spelling.</summary>
    public static string? NormalizeCasing(string content, string canonicalId)
    {
        if (!MsBuildXml.TryParse(content, out var document, out _))
        {
            return null;
        }

        var changed = false;

        foreach (var element in MsBuildXml.Descendants(document, "PackageVersion"))
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

    /// <summary>Removes duplicate <c>PackageVersion</c> entries that differ only by casing.</summary>
    public static string? DeduplicateEntries(string content)
    {
        if (!MsBuildXml.TryParse(content, out var document, out _))
        {
            return null;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var element in MsBuildXml.Descendants(document, "PackageVersion").ToList())
        {
            var id = MsBuildXml.AttributeValue(element, "Include");
            if (string.IsNullOrWhiteSpace(id) || seen.Add(id))
            {
                continue;
            }

            var parent = element.Parent;
            MsBuildXml.RemoveWithWhitespace(element);
            MsBuildXml.RemoveIfEmpty(parent);
            changed = true;
        }

        return changed ? MsBuildXml.ToString(document) : null;
    }

    private static XElement? FindEntry(XDocument document, string packageId) =>
        MsBuildXml
            .Descendants(document, "PackageVersion")
            .FirstOrDefault(e =>
                string.Equals(
                    MsBuildXml.AttributeValue(e, "Include")
                        ?? MsBuildXml.AttributeValue(e, "Update"),
                    packageId,
                    StringComparison.OrdinalIgnoreCase
                )
            );
}
