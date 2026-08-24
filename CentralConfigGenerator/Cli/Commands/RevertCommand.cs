using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Cli.Rendering;
using CentralConfigGenerator.Core.Migration;
using CentralConfigGenerator.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public sealed class RevertSettings : MutatingSettings;

/// <summary>Undoes central package management, writing versions back into the project files.</summary>
public sealed class RevertCommand(IMigrationService migrationService, EffectiveSettings effective)
    : AsyncCommand<RevertSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, RevertSettings settings)
    {
        Ui.Quiet = settings.Quiet;
        Ui.Verbose = settings.Verbose;

        await effective.LoadAsync(settings);

        var options = new MigrationOptions
        {
            RootDirectory = settings.ResolvedDirectory,
            SolutionPath = settings.Solution,
            ProjectPath = settings.Project,
            ExcludePattern = effective.Text(settings.ExcludeDirs, c => c.ExcludeDirs),
            CreateBackup = !settings.NoBackup && effective.File?.Backup != false,
            BackupDirectory = effective.Text(settings.BackupDir, c => c.BackupDir),
        };

        var plan = await Ui.StatusAsync(
            "Reading central versions…",
            async _ => await migrationService.PlanRevertAsync(options)
        );

        if (plan.ProjectCount == 0)
        {
            Ui.Warn("No project files were found.");
            return ExitCode.NoProjectsFound;
        }

        var unresolved = plan
            .Findings.Where(f => f.RuleId == Core.Analysis.RuleIds.MissingCentralVersion)
            .ToList();

        foreach (var finding in unresolved)
        {
            Ui.Warn(finding.Message);
        }

        if (!plan.HasChanges)
        {
            Ui.Success("Nothing to revert.");
            return ExitCode.Success;
        }

        ReportRenderer.RenderPlan(plan, settings.Diff, options.RootDirectory);

        if (settings.DryRun)
        {
            Ui.Blank();
            Ui.Info("[grey]Dry run: no files were written.[/]");
            return ExitCode.Success;
        }

        if (
            unresolved.Count > 0
            && !settings.Force
            && !Ui.Confirm(
                $"{unresolved.Count} package(s) have no central version and will be left without one. Continue?",
                defaultValue: false
            )
        )
        {
            Ui.Warn("Cancelled.");
            return ExitCode.Success;
        }

        if (
            !settings.Force
            && !Ui.Confirm("This deletes Directory.Packages.props and inlines every version. Continue?")
        )
        {
            Ui.Warn("Cancelled.");
            return ExitCode.Success;
        }

        var backup = await migrationService.ApplyAsync(plan, options);

        Ui.Blank();
        Ui.Success($"Reverted {plan.Changes.Count} file(s).");

        if (backup is not null)
        {
            Ui.Info($"[grey]Backup {backup.Id} created.[/]");
        }

        return unresolved.Count > 0 ? ExitCode.VersionConflict : ExitCode.Success;
    }
}
