namespace CentralConfigGenerator.Core.Analysis;

/// <summary>What an auto-fix should do when <c>--fix</c> is supplied.</summary>
public enum FixKind
{
    None = 0,

    /// <summary>Remove an inline <c>Version</c> attribute from a project's PackageReference.</summary>
    RemoveInlineVersion,

    /// <summary>Set (or add) a <c>PackageVersion</c> entry in Directory.Packages.props.</summary>
    SetCentralVersion,

    /// <summary>Delete a duplicate <c>PackageReference</c> element.</summary>
    RemoveDuplicateReference,

    /// <summary>Rewrite a package id so its casing matches the canonical spelling.</summary>
    NormalizePackageCasing,

    /// <summary>Remove an unused <c>PackageVersion</c> entry.</summary>
    RemoveOrphanedPackageVersion,
}

/// <summary>A single analysis result.</summary>
public sealed record Finding
{
    public required string RuleId { get; init; }

    public required Severity Severity { get; init; }

    public required string Message { get; init; }

    public string? PackageId { get; init; }

    public string? Version { get; init; }

    public string? ProjectPath { get; init; }

    /// <summary>
    /// What the finding is about when it is not a package — an MSBuild property name, for
    /// example. Keeps fingerprints unique for rules that report several findings per project.
    /// </summary>
    public string? Subject { get; init; }

    public int Line { get; init; }

    public string? Recommendation { get; init; }

    public FixKind Fix { get; init; } = FixKind.None;

    /// <summary>Target value for the fix (for example the version to pin to, or the canonical id).</summary>
    public string? FixValue { get; init; }

    public bool IsAutoFixable => Fix != FixKind.None;

    /// <summary>Stable key used for baseline suppression.</summary>
    public string Fingerprint =>
        $"{RuleId}|{PackageId ?? Subject ?? "-"}|{NormalizePath(ProjectPath)}|{Version ?? "-"}";

    private static string NormalizePath(string? path) =>
        string.IsNullOrEmpty(path)
            ? "-"
            : Path.GetFileName(path);
}
