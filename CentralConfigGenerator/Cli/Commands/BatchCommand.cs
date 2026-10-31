using System.ComponentModel;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Cli.Rendering;
using CentralConfigGenerator.Core.Migration;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Workspace;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public sealed class BatchSettings : MutatingSettings
{
    [CommandArgument(0, "[ROOT]")]
    [Description("Root directory containing several repositories or solutions.")]
    public string? Root { get; init; }

    [CommandOption("--parallel")]
    [Description("Process workspaces concurrently.")]
    public bool Parallel { get; init; }

    [CommandOption("--continue")]
    [Description("Keep going after a workspace fails.")]
    public bool ContinueOnError { get; init; }

    [CommandOption("--max-depth <N>")]
    [Description("How deep to search for solutions. Default: 4.")]
    public int? MaxDepth { get; init; }

    [CommandOption("--list")]
    [Description("List the workspaces that would be processed, then stop.")]
    public bool List { get; init; }

    [CommandOption("-t|--transitive-pinning")]
    [Description("Enable CentralPackageTransitivePinningEnabled in every generated file.")]
    public bool TransitivePinning { get; init; }
}

/// <summary>Runs the migration across every solution in a monorepo.</summary>
public sealed class BatchCommand(
    BatchService batchService,
    IMigrationService migrationService,
    EffectiveSettings effective
) : AsyncCommand<BatchSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BatchSettings settings, CancellationToken cancellationToken)
    {
        Ui.Quiet = settings.Quiet;
        Ui.Verbose = settings.Verbose;

        await effective.LoadAsync(settings);

        var root = Path.GetFullPath(settings.Root ?? settings.ResolvedDirectory);

        var batchOptions = new BatchOptions
        {
            RootDirectory = root,
            Parallel = settings.Parallel,
            ContinueOnError = settings.ContinueOnError,
            ExcludePattern = effective.Text(settings.ExcludeDirs, c => c.ExcludeDirs),
            MaxDepth = settings.MaxDepth ?? 4,
        };

        var workspaces = batchService.DiscoverWorkspaces(batchOptions);

        if (workspaces.Count == 0)
        {
            Ui.Warn($"No solutions or projects found beneath {ReportRenderer.Relative(root)}.");
            return ExitCode.NoProjectsFound;
        }

        Ui.Info($"Found [bold]{workspaces.Count}[/] workspace(s).");

        if (settings.List)
        {
            foreach (var workspace in workspaces)
            {
                Ui.Info($"  {Markup.Escape(ReportRenderer.Relative(workspace))}");
            }

            return ExitCode.Success;
        }

        if (
            !settings.Force
            && !settings.DryRun
            && !Ui.Confirm($"Migrate all {workspaces.Count} workspace(s)?", defaultValue: false)
        )
        {
            Ui.Warn("Cancelled.");
            return ExitCode.Success;
        }

        var result = await batchService.RunAsync(
            batchOptions,
            async (workspace, token) =>
            {
                var options = new MigrationOptions
                {
                    RootDirectory = workspace,
                    ExcludePattern = batchOptions.ExcludePattern,
                    Merge = true,
                    TransitivePinning = effective.Bool(
                        settings.TransitivePinning,
                        c => c.TransitivePinning
                    ),
                    CreateBackup = !settings.NoBackup && effective.File?.Backup != false,
                    BackupDirectory = settings.BackupDir,
                };

                var plan = await migrationService.PlanAsync(options, null, token);

                if (!settings.DryRun && plan.HasChanges)
                {
                    await migrationService.ApplyAsync(plan, options, token);
                }

                return plan;
            },
            new Progress<string>(workspace =>
                Ui.Detail($"→ {ReportRenderer.Relative(workspace)}")
            )
        );

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Workspace");
        table.AddColumn(new TableColumn("Projects").RightAligned());
        table.AddColumn(new TableColumn("Packages").RightAligned());
        table.AddColumn(new TableColumn("Changes").RightAligned());
        table.AddColumn("Result");

        foreach (var item in result.Items)
        {
            table.AddRow(
                Markup.Escape(ReportRenderer.Relative(item.Workspace)),
                item.Plan?.ProjectCount.ToString() ?? "-",
                item.Plan?.PackageCount.ToString() ?? "-",
                item.Plan?.Changes.Count.ToString() ?? "-",
                item.Succeeded
                    ? settings.DryRun
                        ? "[grey]dry run[/]"
                        : "[green]ok[/]"
                    : $"[red]{Markup.Escape(item.Error ?? "failed")}[/]"
            );
        }

        AnsiConsole.Write(table);
        Ui.Blank();

        if (result.FailedCount > 0)
        {
            Ui.Error($"{result.FailedCount} workspace(s) failed, {result.SucceededCount} succeeded.");
            return ExitCode.UnexpectedError;
        }

        Ui.Success($"{result.SucceededCount} workspace(s) processed.");
        return ExitCode.Success;
    }
}
