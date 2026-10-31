using System.ComponentModel;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Workspace;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public sealed class StatusSettings : WorkspaceSettings
{
    [CommandOption("--audit")]
    [Description("Include security advisories in the dashboard (slower; needs network).")]
    public bool Audit { get; init; }
}

/// <summary>A one-shot dashboard of the workspace and what to do next.</summary>
public sealed class StatusCommand(StatusService statusService) : AsyncCommand<StatusSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StatusSettings settings, CancellationToken cancellationToken)
    {
        Ui.Quiet = settings.Quiet;
        Ui.Verbose = settings.Verbose;

        var options = new AnalysisOptions
        {
            RootDirectory = settings.ResolvedDirectory,
            SolutionPath = settings.Solution,
            ProjectPath = settings.Project,
            ExcludePattern = settings.ExcludeDirs,
            Audit = settings.Audit,
            Deprecated = settings.Audit,
        };

        var status = await Ui.StatusAsync(
            "Inspecting workspace…",
            async _ => await statusService.GetAsync(settings.ResolvedDirectory, options)
        );

        if (status.ProjectCount == 0)
        {
            Ui.Warn("No project files were found here.");
            return ExitCode.NoProjectsFound;
        }

        var workspace = new Grid().AddColumn().AddColumn();
        workspace.AddRow("[bold]Directory[/]", Markup.Escape(status.RootDirectory));
        workspace.AddRow(
            "[bold]Solutions[/]",
            status.Solutions.Count == 0 ? "[grey]none[/]" : Markup.Escape(string.Join(", ", status.Solutions))
        );
        workspace.AddRow(
            "[bold]Projects[/]",
            $"{status.ProjectCount} ({string.Join(", ", status.ProjectKinds.Select(k => $"{k.Value} {k.Key}"))})"
        );
        workspace.AddRow("[bold]Packages[/]", status.PackageCount.ToString());
        workspace.AddRow(
            "[bold]Frameworks[/]",
            status.TargetFrameworks.Count == 0
                ? "[grey]unknown[/]"
                : Markup.Escape(string.Join(", ", status.TargetFrameworks))
        );

        AnsiConsole.Write(new Panel(workspace).Header("[bold]Workspace[/]").Border(BoxBorder.Rounded));

        var config = new Grid().AddColumn().AddColumn();
        config.AddRow("[bold]Central package management[/]", Flag(status.CentralPackageManagementEnabled));
        config.AddRow("[bold]Transitive pinning[/]", Flag(status.TransitivePinningEnabled));
        config.AddRow("[bold]Directory.Build.props[/]", Flag(status.HasBuildProps));
        config.AddRow("[bold].centralconfig.json[/]", Flag(status.HasConfigFile));
        config.AddRow("[bold]Backups[/]", status.BackupCount.ToString());

        AnsiConsole.Write(new Panel(config).Header("[bold]Configuration[/]").Border(BoxBorder.Rounded));

        var health = status.Analysis;
        var color = health.HealthScore switch
        {
            >= 90 => "green",
            >= 75 => "yellow",
            _ => "red",
        };

        var summary = new Grid().AddColumn().AddColumn();
        summary.AddRow(
            "[bold]Score[/]",
            $"[{color}]{health.HealthScore}/100 ({health.Grade})[/]"
        );

        foreach (
            var severity in new[]
            {
                Severity.Critical,
                Severity.High,
                Severity.Moderate,
                Severity.Low,
                Severity.Info,
            }
        )
        {
            var count = health.CountOf(severity);
            if (count > 0)
            {
                summary.AddRow(
                    $"[bold]{Rendering.ReportRenderer.Colorize(severity)}[/]",
                    count.ToString()
                );
            }
        }

        AnsiConsole.Write(new Panel(summary).Header("[bold]Health[/]").Border(BoxBorder.Rounded));

        Ui.Blank();
        AnsiConsole.MarkupLine($"[bold]Next step:[/] {Markup.Escape(status.NextStep)}");

        return ExitCode.Success;
    }

    private static string Flag(bool value) => value ? "[green]yes[/]" : "[grey]no[/]";
}
