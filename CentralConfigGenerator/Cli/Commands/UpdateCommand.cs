using System.ComponentModel;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Workspace;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public sealed class UpdateSettings : MutatingSettings
{
    [CommandOption("--include-prerelease")]
    [Description("Allow pre-release versions as upgrade targets.")]
    public bool IncludePrerelease { get; init; }

    [CommandOption("--bisect")]
    [Description("On test failure, keep the largest subset of updates that still passes.")]
    public bool Bisect { get; init; }

    [CommandOption("--bisect-budget <N>")]
    [Description("Maximum restore+test cycles to spend bisecting. Default: 16.")]
    public int? BisectBudget { get; init; }

    [CommandOption("--bisect-test-filter <EXPRESSION>")]
    [Description("Passed to 'dotnet test --filter' during bisect cycles.")]
    public string? TestFilter { get; init; }

    [CommandOption("--only <PACKAGES>")]
    [Description("Comma-separated package ids to update.")]
    public string? Only { get; init; }

    [CommandOption("--skip-tests")]
    [Description("Apply updates without running the test suite.")]
    public bool SkipTests { get; init; }
}

/// <summary>Moves central package versions forward, with tests as the safety net.</summary>
public sealed class UpdateCommand(IPackageUpdateService updateService, EffectiveSettings effective)
    : AsyncCommand<UpdateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, UpdateSettings settings, CancellationToken cancellationToken)
    {
        Ui.Quiet = settings.Quiet;
        Ui.Verbose = settings.Verbose;

        await effective.LoadAsync(settings);

        var options = new PackageUpdateOptions
        {
            RootDirectory = settings.ResolvedDirectory,
            SolutionPath = settings.Solution,
            DryRun = settings.DryRun,
            IncludePrerelease = settings.IncludePrerelease,
            Bisect = settings.Bisect,
            BisectBudget = settings.BisectBudget ?? 16,
            TestFilter = settings.TestFilter,
            Only = (settings.Only ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            CreateBackup = !settings.NoBackup && effective.File?.Backup != false,
            BackupDirectory = effective.Text(settings.BackupDir, c => c.BackupDir),
            SkipTests = settings.SkipTests,
        };

        var result = await Ui.StatusAsync(
            "Checking for updates…",
            async update => await updateService.UpdateAsync(options, new Progress<string>(update))
        );

        if (result.Candidates.Count == 0)
        {
            Ui.Success(result.Message ?? "Everything is up to date.");
            return ExitCode.Success;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Package");
        table.AddColumn("Current");
        table.AddColumn("New");
        table.AddColumn(new TableColumn("Status").Centered());

        var applied = result.Applied.Select(c => c.PackageId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in result.Candidates)
        {
            var status = settings.DryRun
                ? "[grey]available[/]"
                : applied.Contains(candidate.PackageId)
                    ? "[green]applied[/]"
                    : "[yellow]held back[/]";

            table.AddRow(
                Markup.Escape(candidate.PackageId),
                Markup.Escape(candidate.CurrentVersion),
                candidate.IsMajorUpgrade
                    ? $"[yellow]{Markup.Escape(candidate.NewVersion)}[/] [grey](major)[/]"
                    : Markup.Escape(candidate.NewVersion),
                status
            );
        }

        AnsiConsole.Write(table);
        Ui.Blank();

        if (result.BisectCycles > 0)
        {
            Ui.Info($"[grey]{result.BisectCycles} restore+test cycle(s) used.[/]");
        }

        if (result.Message is not null)
        {
            if (result.RolledBack)
            {
                Ui.Error(result.Message);
            }
            else
            {
                Ui.Success(result.Message);
            }
        }

        if (result.Backup is not null)
        {
            Ui.Info($"[grey]Backup {result.Backup.Id} created.[/]");
        }

        if (result.RolledBack)
        {
            return ExitCode.TestFailure;
        }

        return ExitCode.Success;
    }
}
