using System.ComponentModel;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Core.Migration;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Services.Abstractions;
using CentralConfigGenerator.Core.Workspace;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public sealed class VerifySettings : MutatingSettings
{
    [CommandOption("--strict")]
    [Description("Fail on any graph change at all, including additions.")]
    public bool Strict { get; init; }

    [CommandOption("--keep")]
    [Description("Keep the migration even when drift is detected.")]
    public bool Keep { get; init; }
}

/// <summary>
/// Migrates, then proves the resolved package graph is unchanged. This is the check that turns
/// "the files look right" into "the build produces the same binaries".
/// </summary>
public sealed class VerifyCommand(
    IMigrationService migrationService,
    IVerificationService verificationService,
    IBackupService backupService,
    EffectiveSettings effective
) : AsyncCommand<VerifySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, VerifySettings settings)
    {
        Ui.Quiet = settings.Quiet;
        Ui.Verbose = settings.Verbose;

        await effective.LoadAsync(settings);

        var root = settings.ResolvedDirectory;
        var target = settings.Solution ?? settings.Project ?? root;

        var options = new MigrationOptions
        {
            RootDirectory = root,
            SolutionPath = settings.Solution,
            ProjectPath = settings.Project,
            ExcludePattern = effective.Text(settings.ExcludeDirs, c => c.ExcludeDirs),
            Merge = true,
            CreateBackup = true,
            BackupDirectory = effective.Text(settings.BackupDir, c => c.BackupDir),
        };

        Ui.Rule("Baseline");

        var before = await Ui.StatusAsync(
            "Restoring and snapshotting the current graph…",
            async _ => await verificationService.SnapshotAsync(target, root)
        );

        if (!before.IsAvailable)
        {
            Ui.Error($"Could not snapshot the graph: {before.Error}");
            return ExitCode.IncompleteAnalysis;
        }

        Ui.Success($"Baseline captured: {before.Packages.Count} resolved package(s).");

        var plan = await migrationService.PlanAsync(options);

        if (plan.ProjectCount == 0)
        {
            Ui.Warn("No project files were found.");
            return ExitCode.NoProjectsFound;
        }

        if (!plan.HasChanges)
        {
            Ui.Success("Nothing to migrate — the graph cannot drift.");
            return ExitCode.Success;
        }

        Ui.Rule("Migration");

        var backup = await migrationService.ApplyAsync(plan, options);
        Ui.Success($"Applied {plan.Changes.Count} change(s).");

        Ui.Rule("Verification");

        var after = await Ui.StatusAsync(
            "Restoring and snapshotting the migrated graph…",
            async _ => await verificationService.SnapshotAsync(target, root)
        );

        var result = verificationService.Compare(before, after);

        if (!result.GraphAvailable)
        {
            Ui.Error($"Could not verify: {result.Error}");
            await RollbackAsync(settings, backup, options);
            return ExitCode.IncompleteAnalysis;
        }

        if (result.IsClean)
        {
            Ui.Success(
                $"Graph unchanged: {result.AfterCount} resolved package(s) match the baseline exactly."
            );
            return ExitCode.Success;
        }

        var table = new Table().Border(TableBorder.Rounded).Title("[yellow]Graph drift[/]");
        table.AddColumn("Package");
        table.AddColumn("Before");
        table.AddColumn("After");

        foreach (var drift in result.Drift)
        {
            table.AddRow(
                Markup.Escape(drift.PackageId),
                drift.Before is null ? "[grey]absent[/]" : Markup.Escape(drift.Before),
                drift.After is null ? "[red]removed[/]" : Markup.Escape(drift.After)
            );
        }

        AnsiConsole.Write(table);
        Ui.Blank();

        var onlyAdditions = result.Drift.All(d => d.IsAddition);

        if (!settings.Strict && onlyAdditions)
        {
            Ui.Warn(
                $"{result.Drift.Count} package(s) appeared in the graph, none changed version. "
                    + "This is expected when transitive pinning is enabled."
            );
            return ExitCode.Success;
        }

        Ui.Error($"{result.Drift.Count} package(s) resolve differently after the migration.");

        if (settings.Keep)
        {
            Ui.Warn("Keeping the migration as requested (--keep).");
            return ExitCode.GraphDrift;
        }

        await RollbackAsync(settings, backup, options);
        return ExitCode.GraphDrift;
    }

    private async Task RollbackAsync(
        VerifySettings settings,
        BackupSet? backup,
        MigrationOptions options
    )
    {
        if (backup is null)
        {
            Ui.Warn("No backup was taken, so the migration could not be rolled back automatically.");
            return;
        }

        if (!settings.Force && !Ui.Confirm("Roll the migration back?", defaultValue: true))
        {
            Ui.Warn($"Left in place. Roll back later with: central-config backups restore {backup.Id}");
            return;
        }

        await backupService.RestoreAsync(
            options.BackupDirectory ?? options.RootDirectory,
            backup.Id
        );

        Ui.Success($"Rolled back to backup {backup.Id}.");
    }
}
