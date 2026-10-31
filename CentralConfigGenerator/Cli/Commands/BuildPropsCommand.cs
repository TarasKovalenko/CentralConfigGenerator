using System.ComponentModel;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Cli.Rendering;
using CentralConfigGenerator.Core.Migration;
using CentralConfigGenerator.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public sealed class BuildPropsSettings : MutatingSettings
{
    [CommandOption("-o|--output-dir <PATH>")]
    [Description("Where Directory.Build.props is written. Defaults to the scanned directory.")]
    public string? OutputDir { get; init; }

    [CommandOption("--overwrite")]
    [Description("Replace an existing Directory.Build.props.")]
    public bool Overwrite { get; init; }

    [CommandOption("-m|--merge")]
    [Description("Merge into an existing Directory.Build.props.")]
    public bool Merge { get; init; }

    [CommandOption("--property <NAME>")]
    [Description("Property to consider for hoisting. Repeatable; replaces the default list.")]
    public string[]? Properties { get; init; }

    [CommandOption("--keep-in-projects")]
    [Description("Write Directory.Build.props but leave the projects untouched.")]
    public bool KeepInProjects { get; init; }
}

/// <summary>Hoists MSBuild properties shared by every project into Directory.Build.props.</summary>
public sealed class BuildPropsCommand(
    IMigrationService migrationService,
    EffectiveSettings effective
) : AsyncCommand<BuildPropsSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BuildPropsSettings settings, CancellationToken cancellationToken)
    {
        Ui.Quiet = settings.Quiet;
        Ui.Verbose = settings.Verbose;

        await effective.LoadAsync(settings);

        var options = new MigrationOptions
        {
            RootDirectory = settings.ResolvedDirectory,
            SolutionPath = settings.Solution,
            ProjectPath = settings.Project,
            OutputDirectory = settings.OutputDir,
            ExcludePattern = effective.Text(settings.ExcludeDirs, c => c.ExcludeDirs),
            Merge = settings.Merge,
            // Only the property half of the migration is wanted here.
            KeepInlineVersions = true,
            UnifyProperties = true,
            CreateBackup = !settings.NoBackup && effective.File?.Backup != false,
            BackupDirectory = effective.Text(settings.BackupDir, c => c.BackupDir),
            AddGitIgnore = effective.Bool(settings.AddGitIgnore, c => c.AddGitignore),
            BuildProperties = settings.Properties
                ?? effective.File?.BuildProperties
                ?? BuildPropertyDefaults.Recommended,
        };

        if (!settings.Merge && !settings.Overwrite && !settings.DryRun && File.Exists(options.BuildPropsPath))
        {
            Ui.Warn(
                "Directory.Build.props already exists. Use --merge to combine, or --overwrite to replace it."
            );
            return ExitCode.ValidationError;
        }

        var plan = await Ui.StatusAsync(
            "Looking for shared properties…",
            async _ => await migrationService.PlanAsync(options)
        );

        if (plan.ProjectCount == 0)
        {
            Ui.Warn("No project files were found.");
            return ExitCode.NoProjectsFound;
        }

        // Drop the Directory.Packages.props change; this command is about properties only.
        var changes = plan
            .Changes.Where(c =>
                !string.Equals(c.Path, options.PackagesPropsPath, StringComparison.OrdinalIgnoreCase)
            )
            .Where(c =>
                !settings.KeepInProjects
                || string.Equals(c.Path, options.BuildPropsPath, StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        if (changes.Count == 0)
        {
            Ui.Warn(
                "No property is declared identically by two or more projects, so nothing can be hoisted safely."
            );
            return ExitCode.Success;
        }

        var trimmed = plan with { Changes = changes };

        ReportRenderer.RenderPlan(trimmed, settings.Diff, options.RootDirectory);

        if (settings.DryRun)
        {
            Ui.Blank();
            Ui.Info("[grey]Dry run: no files were written.[/]");
            return ExitCode.Success;
        }

        var backup = await migrationService.ApplyAsync(trimmed, options);

        Ui.Blank();
        Ui.Success($"Wrote {changes.Count} file(s).");

        if (backup is not null)
        {
            Ui.Info($"[grey]Backup {backup.Id} created.[/]");
        }

        return ExitCode.Success;
    }
}
