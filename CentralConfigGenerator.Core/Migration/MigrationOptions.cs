using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Services;
using NuGet.Versioning;

namespace CentralConfigGenerator.Core.Migration;

/// <summary>Everything that shapes a migration to (or away from) central package management.</summary>
public sealed record MigrationOptions
{
    public string RootDirectory { get; init; } = Directory.GetCurrentDirectory();

    public string? SolutionPath { get; init; }

    public string? ProjectPath { get; init; }

    /// <summary>Where Directory.Packages.props is written. Defaults to <see cref="RootDirectory"/>.</summary>
    public string? OutputDirectory { get; init; }

    public string? ExcludePattern { get; init; }

    /// <summary>Merge into an existing Directory.Packages.props instead of replacing it.</summary>
    public bool Merge { get; init; }

    /// <summary>Leave inline <c>Version</c> attributes in place (analysis-only migrations).</summary>
    public bool KeepInlineVersions { get; init; }

    public VersionResolutionStrategy ConflictStrategy { get; init; } =
        VersionResolutionStrategy.Highest;

    /// <summary>Fail the run instead of auto-resolving when projects disagree on a version.</summary>
    public bool FailOnConflict { get; init; }

    public bool IgnorePrerelease { get; init; }

    public bool TransitivePinning { get; init; }

    public VersionComparison VersionComparison { get; init; } = VersionComparison.VersionRelease;

    public LineEndingStyle LineEnding { get; init; } = LineEndingStyle.Preserve;

    public string? EncodingName { get; init; }

    public bool CreateBackup { get; init; } = true;

    public string? BackupDirectory { get; init; }

    public bool AddGitIgnore { get; init; }

    /// <summary>Also promote shared project properties into Directory.Build.props.</summary>
    public bool UnifyProperties { get; init; }

    public IReadOnlyList<string> BuildProperties { get; init; } =
        BuildPropertyDefaults.Recommended;

    public IReadOnlyList<ProjectKind> IncludedKinds { get; init; } =
        [ProjectKind.CSharp, ProjectKind.FSharp, ProjectKind.VisualBasic];

    public string ResolvedOutputDirectory =>
        Path.GetFullPath(
            string.IsNullOrWhiteSpace(OutputDirectory) ? RootDirectory : OutputDirectory
        );

    public string PackagesPropsPath =>
        Path.Combine(ResolvedOutputDirectory, "Directory.Packages.props");

    public string BuildPropsPath => Path.Combine(ResolvedOutputDirectory, "Directory.Build.props");
}

/// <summary>Property names that are safe and useful to hoist into Directory.Build.props.</summary>
public static class BuildPropertyDefaults
{
    public static readonly IReadOnlyList<string> Recommended =
    [
        "TargetFramework",
        "TargetFrameworks",
        "ImplicitUsings",
        "Nullable",
        "LangVersion",
        "AnalysisLevel",
        "EnforceCodeStyleInBuild",
        "TreatWarningsAsErrors",
        "GenerateDocumentationFile",
        "InvariantGlobalization",
        "Authors",
        "Company",
        "Product",
        "Copyright",
        "PackageLicenseExpression",
        "PackageProjectUrl",
        "RepositoryUrl",
        "RepositoryType",
        "Version",
        "VersionPrefix",
    ];

    /// <summary>Properties that must never be hoisted because they are project-identity specific.</summary>
    public static readonly IReadOnlyList<string> NeverHoist =
    [
        "AssemblyName",
        "RootNamespace",
        "OutputType",
        "ProjectGuid",
        "UserSecretsId",
        "StartupObject",
        "PackageId",
        "ApplicationIcon",
        "TargetFrameworkIdentifier",
        "TargetFrameworkVersion",
    ];
}
