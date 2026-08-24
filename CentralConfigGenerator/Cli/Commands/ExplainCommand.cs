using System.ComponentModel;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Cli.Rendering;
using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public sealed class ExplainSettings : CommandSettings
{
    [CommandArgument(0, "[RULE]")]
    [Description("Rule id to explain, or 'all' to list every rule.")]
    public string? Rule { get; init; }
}

/// <summary>Documents what a rule looks for and why it matters.</summary>
public sealed class ExplainCommand : Command<ExplainSettings>
{
    public override int Execute(CommandContext context, ExplainSettings settings)
    {
        var rules = RuleCatalog.CreateAll();

        if (settings.Rule is null || string.Equals(settings.Rule, "all", StringComparison.OrdinalIgnoreCase))
        {
            var table = new Table().Border(TableBorder.Rounded).Expand();
            table.AddColumn("Rule");
            table.AddColumn("Default");
            table.AddColumn("What it finds");
            table.AddColumn(new TableColumn("Needs").Centered());

            foreach (var rule in rules)
            {
                table.AddRow(
                    Markup.Escape(rule.RuleId),
                    ReportRenderer.Colorize(rule.DefaultSeverity),
                    Markup.Escape(rule.Description),
                    rule.RequiresNetwork ? "[yellow]feed[/]" : "[grey]-[/]"
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine(
                "\n[grey]Run 'central-config explain <RuleId>' for the full rationale, "
                    + "or set severities with --rules RuleId=Severity (or =none to disable).[/]"
            );

            return ExitCode.Success;
        }

        var match = RuleCatalog.Find(settings.Rule);

        if (match is null)
        {
            Ui.Error($"Unknown rule '{settings.Rule}'.");

            var suggestion = rules
                .Select(r => r.RuleId)
                .FirstOrDefault(id => id.Contains(settings.Rule, StringComparison.OrdinalIgnoreCase));

            if (suggestion is not null)
            {
                Ui.Info($"Did you mean [bold]{suggestion}[/]?");
            }

            Ui.Info("[grey]Run 'central-config explain all' to list every rule.[/]");
            return ExitCode.ValidationError;
        }

        var grid = new Grid().AddColumn(new GridColumn().NoWrap().PadRight(2)).AddColumn();
        grid.AddRow("[bold]Rule[/]", Markup.Escape(match.RuleId));
        grid.AddRow("[bold]Title[/]", Markup.Escape(match.Title));
        grid.AddRow("[bold]Default severity[/]", ReportRenderer.Colorize(match.DefaultSeverity));
        grid.AddRow("[bold]Needs the feed[/]", match.RequiresNetwork ? "yes" : "no");
        grid.AddRow("[bold]Needs CPM[/]", match.RequiresCentralPackageManagement ? "yes" : "no");
        grid.AddRow("[bold]Detects[/]", Markup.Escape(match.Description));
        grid.AddRow("[bold]Why it matters[/]", Markup.Escape(match.Rationale));

        AnsiConsole.Write(new Panel(grid).Header($"[bold]{Markup.Escape(match.Title)}[/]").Border(BoxBorder.Rounded));

        return ExitCode.Success;
    }
}
