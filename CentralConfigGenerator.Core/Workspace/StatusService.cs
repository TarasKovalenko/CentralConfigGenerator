using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.IO;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Services;

namespace CentralConfigGenerator.Core.Workspace;

/// <summary>One-shot snapshot of a workspace: what is there and how healthy it is.</summary>
public sealed record WorkspaceStatus
{
    public required string RootDirectory { get; init; }

    public required int ProjectCount { get; init; }

    public required int PackageCount { get; init; }

    public required bool CentralPackageManagementEnabled { get; init; }

    public required bool TransitivePinningEnabled { get; init; }

    public bool HasBuildProps { get; init; }

    public bool HasConfigFile { get; init; }

    public int BackupCount { get; init; }

    public IReadOnlyList<string> Solutions { get; init; } = [];

    public IReadOnlyDictionary<ProjectKind, int> ProjectKinds { get; init; } =
        new Dictionary<ProjectKind, int>();

    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];

    public required AnalysisReport Analysis { get; init; }

    /// <summary>The single next action that would most improve this workspace.</summary>
    public string NextStep =>
        !CentralPackageManagementEnabled
            ? "Run 'central-config migrate --dry-run --diff' to preview central package management."
            : Analysis.CountOf(Severity.Critical) > 0
                ? "Run 'central-config analyze --audit' — there are critical findings."
                : Analysis.Findings.Count(f => f.IsAutoFixable) > 0
                    ? "Run 'central-config analyze --fix' to apply the auto-fixable findings."
                    : !HasBuildProps
                        ? "Run 'central-config build' to centralise shared MSBuild properties."
                        : "Nothing urgent. Consider 'central-config update --bisect' to move versions forward.";
}

/// <summary>Builds the <c>status</c> dashboard from a fast, offline-only analysis pass.</summary>
public sealed class StatusService(
    IAnalysisEngine analysisEngine,
    IFileSystem fileSystem,
    Configuration.ConfigurationLoader configurationLoader
)
{
    public async Task<WorkspaceStatus> GetAsync(
        string rootDirectory,
        AnalysisOptions? analysisOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        var root = Path.GetFullPath(rootDirectory);
        var options = analysisOptions ?? new AnalysisOptions { RootDirectory = root };

        var context = await analysisEngine.BuildContextAsync(options, null, cancellationToken);
        var report = await analysisEngine.AnalyzeAsync(options, null, cancellationToken);

        var backupRoot = Path.Combine(root, BackupService.DefaultBackupFolderName);

        return new WorkspaceStatus
        {
            RootDirectory = root,
            ProjectCount = context.Projects.Count,
            PackageCount = context
                .References.Select(r => r.PackageId)
                .Concat(context.CentralVersions.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            CentralPackageManagementEnabled = context.CentralPackageManagementEnabled,
            TransitivePinningEnabled = context.TransitivePinningEnabled,
            HasBuildProps = fileSystem.FileExists(Path.Combine(root, "Directory.Build.props")),
            HasConfigFile = configurationLoader.Find(root) is not null,
            BackupCount = fileSystem.DirectoryExists(backupRoot)
                ? fileSystem.EnumerateDirectories(backupRoot).Count
                : 0,
            Solutions = fileSystem
                .EnumerateFiles(root, "*", recursive: false)
                .Where(SolutionReader.IsSolution)
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Select(n => n!)
                .ToList(),
            ProjectKinds = context
                .Projects.GroupBy(p => p.ResolvedKind)
                .ToDictionary(g => g.Key, g => g.Count()),
            TargetFrameworks = context
                .TargetFrameworks.SelectMany(pair => pair.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Analysis = report,
        };
    }
}
