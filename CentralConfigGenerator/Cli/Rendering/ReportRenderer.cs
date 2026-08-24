using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Migration;
using Spectre.Console;

namespace CentralConfigGenerator.Cli.Rendering;

/// <summary>Renders analysis reports, migration plans and diffs to the terminal.</summary>
public static class ReportRenderer
{
    public static void RenderAnalysis(AnalysisReport report)
    {
        RenderHealthBanner(report);

        if (report.Findings.Count == 0)
        {
            Ui.Success("No findings. Dependencies look healthy.");
            RenderIncompletions(report);
            return;
        }

        var summary = new Table().Border(TableBorder.Rounded).Title("[bold]Findings by severity[/]");
        summary.AddColumn("Severity");
        summary.AddColumn(new TableColumn("Count").RightAligned());

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
            var count = report.CountOf(severity);
            if (count > 0)
            {
                summary.AddRow(Colorize(severity), count.ToString());
            }
        }

        Ui.Write(summary);
        Ui.Blank();

        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("Severity");
        table.AddColumn("Rule");
        table.AddColumn("Package");
        table.AddColumn("Detail");
        table.AddColumn(new TableColumn("Fix").Centered());

        foreach (var finding in report.Findings)
        {
            table.AddRow(
                Colorize(finding.Severity),
                Markup.Escape(finding.RuleId),
                Markup.Escape(finding.PackageId ?? Path.GetFileName(finding.ProjectPath ?? "-")),
                finding.Recommendation is null
                    ? Markup.Escape(finding.Message)
                    : $"{Markup.Escape(finding.Message)} [grey]{Markup.Escape(finding.Recommendation)}[/]",
                finding.IsAutoFixable ? "[green]auto[/]" : "[grey]manual[/]"
            );
        }

        Ui.Write(table);

        var fixable = report.Findings.Count(f => f.IsAutoFixable);
        if (fixable > 0)
        {
            Ui.Blank();
            Ui.Info($"[green]{fixable}[/] finding(s) can be fixed automatically: run with [bold]--fix[/].");
        }

        if (report.SuppressedCount > 0)
        {
            Ui.Info($"[grey]{report.SuppressedCount} finding(s) suppressed by the baseline.[/]");
        }

        RenderIncompletions(report);
    }

    public static void RenderHealthBanner(AnalysisReport report)
    {
        var color = report.HealthScore switch
        {
            >= 90 => "green",
            >= 75 => "yellow",
            _ => "red",
        };

        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow("[bold]Health score[/]", $"[{color}]{report.HealthScore}/100 ({report.Grade})[/]");
        grid.AddRow("[bold]Projects[/]", report.ProjectCount.ToString());
        grid.AddRow("[bold]Packages[/]", report.PackageCount.ToString());
        grid.AddRow("[bold]Findings[/]", report.Findings.Count.ToString());
        grid.AddRow("[bold]Duration[/]", $"{report.Duration.TotalSeconds:F1}s");

        Ui.Write(
            new Panel(grid)
                .Header("[bold]Dependency health[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey)
        );
        Ui.Blank();
    }

    public static void RenderPlan(MigrationPlan plan, bool showDiff, string? root = null)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Change");
        table.AddColumn("File");

        foreach (var change in plan.Changes.OrderBy(c => c.Kind).ThenBy(c => c.Path))
        {
            var label = change.Kind switch
            {
                FileChangeKind.Created => "[green]create[/]",
                FileChangeKind.Deleted => "[red]delete[/]",
                _ => "[yellow]modify[/]",
            };

            table.AddRow(label, Markup.Escape(Relative(change.Path, root)));
        }

        Ui.Write(table);

        if (!showDiff)
        {
            return;
        }

        foreach (var change in plan.Changes)
        {
            var diff = change.Diff();
            if (string.IsNullOrWhiteSpace(diff))
            {
                continue;
            }

            Ui.Blank();
            RenderDiff(diff.Replace(change.Path, Relative(change.Path, root)));
        }
    }

    public static void RenderDiff(string diff)
    {
        foreach (var line in diff.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            var markup = line[0] switch
            {
                '+' when !line.StartsWith("+++", StringComparison.Ordinal) => "green",
                '-' when !line.StartsWith("---", StringComparison.Ordinal) => "red",
                '@' => "cyan",
                _ => "grey",
            };

            AnsiConsole.MarkupLine($"[{markup}]{Markup.Escape(line)}[/]");
        }
    }

    public static void RenderConflicts(IReadOnlyList<PackageConflict> conflicts)
    {
        if (conflicts.Count == 0)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Rounded).Title("[yellow]Version conflicts[/]");
        table.AddColumn("Package");
        table.AddColumn("Versions found");
        table.AddColumn("Resolved to");
        table.AddColumn(new TableColumn("Projects").RightAligned());

        foreach (var conflict in conflicts.OrderBy(c => c.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(
                Markup.Escape(conflict.PackageId),
                Markup.Escape(string.Join(", ", conflict.DistinctVersions)),
                $"[green]{Markup.Escape(conflict.ResolvedVersion)}[/]",
                conflict.References.Count.ToString()
            );
        }

        Ui.Write(table);
        Ui.Blank();
    }

    private static void RenderIncompletions(AnalysisReport report)
    {
        if (!report.IsIncomplete)
        {
            return;
        }

        Ui.Blank();
        Ui.Warn("The scan did not complete fully:");
        foreach (var incompletion in report.Incompletions)
        {
            Ui.Warn($"  {incompletion}");
        }
    }

    public static string Colorize(Severity severity) =>
        severity switch
        {
            Severity.Critical => "[red bold]Critical[/]",
            Severity.High => "[red]High[/]",
            Severity.Moderate => "[yellow]Moderate[/]",
            Severity.Low => "[blue]Low[/]",
            Severity.Info => "[grey]Info[/]",
            _ => "[grey]None[/]",
        };

    /// <summary>Shortens a path against the scanned root, falling back to the absolute form.</summary>
    public static string Relative(string path, string? root = null)
    {
        try
        {
            var basePath = string.IsNullOrEmpty(root) ? Directory.GetCurrentDirectory() : root;
            var relative = Path.GetRelativePath(basePath, path);
            return relative.StartsWith("..", StringComparison.Ordinal) ? path : relative;
        }
        catch (Exception)
        {
            return path;
        }
    }
}
