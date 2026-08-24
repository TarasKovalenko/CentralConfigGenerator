namespace CentralConfigGenerator.Core.Models;

/// <summary>
/// A single <c>PackageReference</c> (or <c>GlobalPackageReference</c>) as declared by a project.
/// </summary>
public sealed record PackageReferenceInfo
{
    public required string PackageId { get; init; }

    /// <summary>The declared version string, or <c>null</c> when the reference has no version (CPM style).</summary>
    public string? Version { get; init; }

    public required string ProjectPath { get; init; }

    /// <summary>The MSBuild <c>Condition</c> on the reference or its item group, when present.</summary>
    public string? Condition { get; init; }

    /// <summary>True when the version came from a child element rather than an attribute.</summary>
    public bool VersionIsElement { get; init; }

    /// <summary>True for <c>GlobalPackageReference</c> items.</summary>
    public bool IsGlobal { get; init; }

    /// <summary>True when the reference targets a private/development dependency.</summary>
    public bool IsPrivateAssets { get; init; }
}
