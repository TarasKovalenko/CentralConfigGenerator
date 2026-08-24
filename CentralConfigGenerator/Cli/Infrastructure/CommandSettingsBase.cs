using System.ComponentModel;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Infrastructure;

/// <summary>Options shared by every command that touches a workspace.</summary>
public class WorkspaceSettings : CommandSettings
{
    [CommandOption("-d|--directory <PATH>")]
    [Description("Directory to scan. Defaults to the current directory.")]
    public string? Directory { get; init; }

    [CommandOption("-s|--solution <PATH>")]
    [Description("Scope the run to a .sln, .slnx or .slnf file.")]
    public string? Solution { get; init; }

    [CommandOption("--project <PATH>")]
    [Description("Scope the run to a single project file.")]
    public string? Project { get; init; }

    [CommandOption("-x|--exclude-dirs <REGEX>")]
    [Description("Regex of directories to skip. bin/obj/node_modules are always skipped.")]
    public string? ExcludeDirs { get; init; }

    [CommandOption("-q|--quiet")]
    [Description("Suppress non-essential output.")]
    public bool Quiet { get; init; }

    [CommandOption("-v|--verbose")]
    [Description("Print diagnostic detail.")]
    public bool Verbose { get; init; }

    [CommandOption("--no-config")]
    [Description("Ignore any .centralconfig.json discovered on disk.")]
    public bool NoConfig { get; init; }

    /// <summary>
    /// The directory the command actually operates on: an explicit --directory, otherwise the
    /// folder holding --solution, otherwise the current directory. Always without a trailing
    /// separator so paths compare and print consistently.
    /// </summary>
    public string ResolvedDirectory
    {
        get
        {
            var directory =
                Directory
                ?? (Solution is not null
                    ? System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Solution))
                    : null)
                ?? System.IO.Directory.GetCurrentDirectory();

            var full = System.IO.Path.GetFullPath(directory);
            var trimmed = full.TrimEnd(
                System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar
            );

            // Guard against trimming a root such as "/" or "C:\" down to nothing.
            return trimmed.Length == 0 ? full : trimmed;
        }
    }

    public override Spectre.Console.ValidationResult Validate()
    {
        if (Directory is not null && !System.IO.Directory.Exists(Directory))
        {
            return Spectre.Console.ValidationResult.Error($"Directory not found: {Directory}");
        }

        if (Solution is not null && !System.IO.File.Exists(Solution) && !System.IO.Directory.Exists(Solution))
        {
            return Spectre.Console.ValidationResult.Error($"Solution not found: {Solution}");
        }

        if (Project is not null && !System.IO.File.Exists(Project) && !System.IO.Directory.Exists(Project))
        {
            return Spectre.Console.ValidationResult.Error($"Project not found: {Project}");
        }

        return Spectre.Console.ValidationResult.Success();
    }
}

/// <summary>Backup and rollback options, shared by every mutating command.</summary>
public class MutatingSettings : WorkspaceSettings
{
    [CommandOption("-n|--dry-run")]
    [Description("Preview the change without writing anything.")]
    public bool DryRun { get; init; }

    [CommandOption("--diff")]
    [Description("Print a unified diff of every pending change.")]
    public bool Diff { get; init; }

    [CommandOption("--no-backup")]
    [Description("Do not create a rollback backup before writing.")]
    public bool NoBackup { get; init; }

    [CommandOption("--backup-dir <PATH>")]
    [Description("Where backups are stored. Defaults to the scanned directory.")]
    public string? BackupDir { get; init; }

    [CommandOption("--add-gitignore")]
    [Description("Add the backup directory to .gitignore.")]
    public bool AddGitIgnore { get; init; }

    [CommandOption("-f|--force")]
    [Description("Skip confirmation prompts.")]
    public bool Force { get; init; }
}
