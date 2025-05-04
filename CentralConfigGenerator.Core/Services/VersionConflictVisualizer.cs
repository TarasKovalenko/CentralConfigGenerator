using CentralConfigGenerator.Core.Analyzers;
using CentralConfigGenerator.Core.Services.Abstractions;
using Spectre.Console;

namespace CentralConfigGenerator.Core.Services;

public class VersionConflictVisualizer : IVersionConflictVisualizer
{
    public void DisplayResults(PackageAnalysisResult result)
    {
        // Display summary
        var summaryTable = new Table();
        summaryTable.AddColumn("Metric");
        summaryTable.AddColumn(new TableColumn("Value").Centered());

        summaryTable.AddRow("Total Packages", result.ResolvedVersions.Count.ToString());
        summaryTable.AddRow("Packages with Conflicts", result.Conflicts.Count.ToString());
        summaryTable.AddRow("Warnings", result.Warnings.Count.ToString());

        AnsiConsole.Write(new Panel(summaryTable)
            .Header("[bold green]Package Analysis Summary[/]")
            .Border(BoxBorder.Rounded));

        AnsiConsole.WriteLine();

        // Display conflicts
        if (result.Conflicts.Count != 0)
        {
            AnsiConsole.MarkupLine("[bold red]Version Conflicts Detected:[/]");

            var conflictTable = new Table();
            conflictTable.AddColumn("Package");
            conflictTable.AddColumn("Project");
            conflictTable.AddColumn("Version");
            conflictTable.AddColumn("Type");

            foreach (var conflict in result.Conflicts)
            {
                foreach (var detail in conflict.Value)
                {
                    string versionType;
                    if (detail.IsRange)
                    {
                        versionType = "Range";
                    }
                    else if (detail.IsPreRelease)
                    {
                        versionType = "Pre-release";
                    }
                    else
                    {
                        versionType = "Release";
                    }

                    conflictTable.AddRow(
                        conflict.Key,
                        Markup.Escape(Path.GetFileName(detail.ProjectFile)),
                        detail.Version,
                        versionType
                    );
                }

                // Add separator row
                if (conflict.Key != result.Conflicts.Keys.Last())
                {
                    conflictTable.AddEmptyRow();
                }
            }

            AnsiConsole.Write(conflictTable);
            AnsiConsole.WriteLine();
        }

        // Display warnings
        if (result.Warnings.Count != 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]Warnings:[/]");

            var warningsTable = new Table();
            warningsTable.AddColumn("Level");
            warningsTable.AddColumn("Package");
            warningsTable.AddColumn("Message");

            foreach (var warning in result.Warnings.OrderBy(w => w.Level))
            {
                var levelMarkup = warning.Level switch
                {
                    WarningLevel.Info => "[blue]Info[/]",
                    WarningLevel.Warning => "[yellow]Warning[/]",
                    WarningLevel.Error => "[red]Error[/]",
                    _ => "[white]Unknown[/]"
                };

                warningsTable.AddRow(
                    levelMarkup,
                    warning.PackageName,
                    Markup.Escape(warning.Message)
                );
            }

            AnsiConsole.Write(warningsTable);
            AnsiConsole.WriteLine();
        }

        // Display resolved versions
        AnsiConsole.MarkupLine("[bold green]Resolved Package Versions:[/]");

        var versionTable = new Table();
        versionTable.AddColumn("Package");
        versionTable.AddColumn("Version");

        foreach (var package in result.ResolvedVersions.OrderBy(p => p.Key))
        {
            versionTable.AddRow(package.Key, package.Value);
        }

        AnsiConsole.Write(versionTable);
    }
}