namespace CentralConfigGenerator.Core.NuGet;

/// <summary>A resolved dependency as reported by <c>dotnet list package</c>.</summary>
public sealed record ResolvedPackage
{
    public required string PackageId { get; init; }

    public required string ResolvedVersion { get; init; }

    public string? RequestedVersion { get; init; }

    public required string ProjectPath { get; init; }

    public required string TargetFramework { get; init; }

    public bool IsTransitive { get; init; }
}

/// <summary>The full package graph for a workspace, keyed for quick lookups.</summary>
public sealed record PackageGraph
{
    public static readonly PackageGraph Empty = new()
    {
        Packages = [],
        IsAvailable = false,
        Error = "Package graph was not collected.",
    };

    public required IReadOnlyList<ResolvedPackage> Packages { get; init; }

    public bool IsAvailable { get; init; }

    public string? Error { get; init; }

    public IEnumerable<ResolvedPackage> Direct => Packages.Where(p => !p.IsTransitive);

    public IEnumerable<ResolvedPackage> Transitive => Packages.Where(p => p.IsTransitive);

    /// <summary>Package ids resolved to more than one version across the workspace.</summary>
    public IEnumerable<IGrouping<string, ResolvedPackage>> DivergentPackages =>
        Packages
            .GroupBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase)
            .Where(g =>
                g.Select(p => p.ResolvedVersion)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() > 1
            );

    /// <summary>A stable signature of the resolved graph, used by <c>verify</c> to detect drift.</summary>
    public IReadOnlyDictionary<string, string> Signature() =>
        Packages
            .GroupBy(
                p => $"{NormalizeProject(p.ProjectPath)}|{p.TargetFramework}|{p.PackageId}",
                StringComparer.OrdinalIgnoreCase
            )
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(p => p.ResolvedVersion, StringComparer.OrdinalIgnoreCase).Last().ResolvedVersion,
                StringComparer.OrdinalIgnoreCase
            );

    private static string NormalizeProject(string path) => Path.GetFileName(path);
}
