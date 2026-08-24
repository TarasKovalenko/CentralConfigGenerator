using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Migration;

public enum FileChangeKind
{
    Created,
    Modified,
    Deleted,
}

/// <summary>A single pending file write, kept in memory so dry-run and diff cost nothing extra.</summary>
public sealed record FileChange
{
    public required string Path { get; init; }

    public required FileChangeKind Kind { get; init; }

    public string OriginalContent { get; init; } = string.Empty;

    public string NewContent { get; init; } = string.Empty;

    public required IO.TextFileFormat Format { get; init; }

    public string Diff(int context = 3) =>
        UnifiedDiffFor(this, context);

    private static string UnifiedDiffFor(FileChange change, int context) =>
        Services.UnifiedDiff.Create(
            change.OriginalContent,
            change.NewContent,
            change.Kind == FileChangeKind.Created ? "/dev/null" : change.Path,
            change.Kind == FileChangeKind.Deleted ? "/dev/null" : change.Path,
            context
        );
}

/// <summary>A version disagreement between two or more projects.</summary>
public sealed record PackageConflict
{
    public required string PackageId { get; init; }

    public required IReadOnlyList<PackageReferenceInfo> References { get; init; }

    public required string ResolvedVersion { get; init; }

    public IEnumerable<string> DistinctVersions =>
        References
            .Select(r => r.Version)
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
}

/// <summary>The complete, still-unapplied result of planning a migration.</summary>
public sealed record MigrationPlan
{
    public required IReadOnlyList<FileChange> Changes { get; init; }

    public required IReadOnlyDictionary<string, string> ResolvedVersions { get; init; }

    public required IReadOnlyList<PackageConflict> Conflicts { get; init; }

    public required IReadOnlyList<Finding> Findings { get; init; }

    public required int ProjectCount { get; init; }

    public int PackageCount => ResolvedVersions.Count;

    public bool HasChanges => Changes.Count > 0;

    public IReadOnlyList<string> AffectedPaths =>
        Changes.Select(c => c.Path).ToList();
}
