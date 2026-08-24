using System.Text.RegularExpressions;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Discovery;

/// <summary>
/// Controls which MSBuild files a scan picks up.
/// </summary>
public sealed record DiscoveryOptions
{
    public static readonly string[] DefaultExcludedDirectories =
    [
        "bin",
        "obj",
        ".git",
        ".vs",
        ".idea",
        "node_modules",
        "packages",
        "artifacts",
        "TestResults",
        // Our own rollback copies are full project trees; scanning them would double-count
        // every project and let a fix rewrite the backup it is supposed to restore from.
        ".centralconfig-backups",
    ];

    /// <summary>Root directory to scan. Ignored when <see cref="SolutionPath"/> is set.</summary>
    public string RootDirectory { get; init; } = Directory.GetCurrentDirectory();

    /// <summary>Optional solution (.sln/.slnx) or solution filter (.slnf) that scopes the scan.</summary>
    public string? SolutionPath { get; init; }

    /// <summary>Optional single project path that scopes the scan.</summary>
    public string? ProjectPath { get; init; }

    /// <summary>Regex applied to directory names and relative paths; matches are skipped.</summary>
    public string? ExcludePattern { get; init; }

    /// <summary>Project kinds to include. Defaults to C#, F# and VB.</summary>
    public IReadOnlyCollection<ProjectKind> IncludedKinds { get; init; } =
        [ProjectKind.CSharp, ProjectKind.FSharp, ProjectKind.VisualBasic];

    public bool Recursive { get; init; } = true;

    /// <summary>When false, the built-in bin/obj/node_modules exclusions are not applied.</summary>
    public bool ApplyDefaultExclusions { get; init; } = true;

    public Regex? CompileExcludeRegex() =>
        string.IsNullOrWhiteSpace(ExcludePattern)
            ? null
            : new Regex(ExcludePattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
