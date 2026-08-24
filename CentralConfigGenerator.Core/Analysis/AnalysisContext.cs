using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.NuGet;

namespace CentralConfigGenerator.Core.Analysis;

/// <summary>Everything the rules read from. Built once per run, then shared by all analyzers.</summary>
public sealed record AnalysisContext
{
    public required AnalysisOptions Options { get; init; }

    public required IReadOnlyList<ProjectFile> Projects { get; init; }

    public required IReadOnlyList<PackageReferenceInfo> References { get; init; }

    /// <summary>Versions declared in Directory.Packages.props, empty when the file is absent.</summary>
    public required IReadOnlyDictionary<string, string> CentralVersions { get; init; }

    public bool CentralPackageManagementEnabled { get; init; }

    public bool TransitivePinningEnabled { get; init; }

    public string? PackagesPropsPath { get; init; }

    public IReadOnlyDictionary<string, PackageMetadataSnapshot> Metadata { get; init; } =
        new Dictionary<string, PackageMetadataSnapshot>(StringComparer.OrdinalIgnoreCase);

    public PackageGraph Graph { get; init; } = PackageGraph.Empty;

    /// <summary>Target frameworks declared per project, used by the framework alignment rule.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> TargetFrameworks { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<PackageReferenceInfo> RelevantReferences =>
        References.Where(r => !IsIgnored(r.PackageId));

    public bool IsIgnored(string packageId) =>
        Options.IgnorePackages.Any(pattern =>
            pattern.EndsWith('*')
                ? packageId.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
                : string.Equals(pattern, packageId, StringComparison.OrdinalIgnoreCase)
        );

    public PackageMetadataSnapshot? MetadataFor(string packageId) =>
        Metadata.TryGetValue(packageId, out var snapshot) ? snapshot : null;

    /// <summary>The version a package effectively resolves to: central entry first, then inline.</summary>
    public string? EffectiveVersion(string packageId)
    {
        if (CentralVersions.TryGetValue(packageId, out var central))
        {
            return central;
        }

        return References
            .Where(r =>
                string.Equals(r.PackageId, packageId, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(r.Version)
            )
            .Select(r => r.Version)
            .FirstOrDefault();
    }
}
