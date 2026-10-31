using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Workspace;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public sealed class DoctorSettings : WorkspaceSettings;

/// <summary>Environment diagnostics: run this first when something behaves unexpectedly.</summary>
public sealed class DoctorCommand(DoctorService doctorService) : AsyncCommand<DoctorSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DoctorSettings settings, CancellationToken cancellationToken)
    {
        Ui.Quiet = settings.Quiet;
        Ui.Verbose = settings.Verbose;

        var report = await Ui.StatusAsync(
            "Running diagnostics…",
            async _ => await doctorService.RunAsync(settings.ResolvedDirectory)
        );

        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn(new TableColumn("").Width(3));
        table.AddColumn("Check");
        table.AddColumn("Result");

        foreach (var check in report.Checks)
        {
            var detail = check.Remedy is null
                ? Markup.Escape(check.Detail)
                : $"{Markup.Escape(check.Detail)}\n[grey]→ {Markup.Escape(check.Remedy)}[/]";

            table.AddRow(Icon(check.Status), Markup.Escape(check.Name), detail);
        }

        AnsiConsole.Write(table);

        if (report.HasFailures)
        {
            Ui.Blank();
            Ui.Error("One or more checks failed. Fix those before migrating.");
            return ExitCode.ValidationError;
        }

        Ui.Blank();
        Ui.Success(
            report.HasWarnings
                ? "No blocking problems, but some checks have suggestions."
                : "Everything looks good."
        );

        return ExitCode.Success;
    }

    private static string Icon(CheckStatus status) =>
        status switch
        {
            CheckStatus.Pass => "[green]✓[/]",
            CheckStatus.Warn => "[yellow]![/]",
            CheckStatus.Fail => "[red]✗[/]",
            _ => "[grey]-[/]",
        };
}
