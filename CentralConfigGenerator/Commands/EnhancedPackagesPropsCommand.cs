using System.Xml.Linq;
using CentralConfigGenerator.Core.Analyzers;
using CentralConfigGenerator.Core.Analyzers.Abstractions;
using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Core.Generators.Abstractions;
using CentralConfigGenerator.Core.Services;
using CentralConfigGenerator.Core.Services.Abstractions;
using CentralConfigGenerator.Services.Abstractions;
using Spectre.Console;

namespace CentralConfigGenerator.Commands;

public class EnhancedPackagesPropsCommand(
    IEnhancedPackageAnalyzer packageAnalyzer,
    IProjectFileService projectFileService,
    IPackagesPropsGenerator packagesPropsGenerator,
    IFileService fileService,
    IVersionConflictVisualizer conflictVisualizer
)
{
    public async Task ExecuteAsync(DirectoryInfo directory, bool overwrite, bool verbose = false)
    {
        AnsiConsole
            .Status()
            .Start(
                "Scanning for project files...",
                ctx =>
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));
                }
            );

        var targetPath = Path.Combine(directory.FullName, "Directory.Packages.props");

        if (fileService.Exists(targetPath) && !overwrite)
        {
            AnsiConsole.MarkupLine(
                "[yellow]Warning:[/] File Directory.Packages.props already exists. Use --overwrite to replace it."
            );
            return;
        }

        var projectFiles = await projectFileService.ScanDirectoryForProjectsAsync(directory);

        if (projectFiles.Count == 0)
        {
            AnsiConsole.MarkupLine(
                "[yellow]Warning:[/] No .csproj files found in the directory tree."
            );
            return;
        }

        AnsiConsole.MarkupLine($"[green]Found {projectFiles.Count} project files[/]");

        // Analyze packages with enhanced analyzer
        var analysisResult = await AnsiConsole
            .Status()
            .StartAsync(
                "Analyzing package versions...",
                async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));
                    return await packageAnalyzer.AnalyzePackagesAsync(projectFiles);
                }
            );

        // Display analysis results
        conflictVisualizer.DisplayResults(analysisResult);

        // Ask for confirmation if conflicts exist
        if (analysisResult.Conflicts.Any())
        {
            if (
                !AnsiConsole.Confirm(
                    "Version conflicts were detected. Continue with resolved versions?"
                )
            )
            {
                AnsiConsole.MarkupLine("[red]Operation cancelled by user.[/]");
                return;
            }
        }

        // Generate Directory.Packages.props
        var packagesPropsContent = packagesPropsGenerator.GeneratePackagesPropsContent(
            analysisResult.ResolvedVersions
        );
        await fileService.WriteAllTextAsync(targetPath, packagesPropsContent);

        AnsiConsole.MarkupLine($"[green]Created Directory.Packages.props at {targetPath}[/]");

        // Update project files
        var updateConfirmed = AnsiConsole.Confirm("Remove version attributes from project files?");
        if (!updateConfirmed)
        {
            AnsiConsole.MarkupLine(
                "[yellow]Skipping project file updates. You'll need to manually remove Version attributes.[/]"
            );
            return;
        }

        await AnsiConsole
            .Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask(
                    "[green]Updating project files[/]",
                    maxValue: projectFiles.Count
                );

                foreach (var projectFile in projectFiles)
                {
                    try
                    {
                        var xDoc = XDocument.Parse(projectFile.Content);
                        var changed = false;

                        var packageReferences = xDoc.Descendants("PackageReference").ToList();

                        foreach (var packageRef in packageReferences)
                        {
                            var versionAttr = packageRef.Attribute("Version");
                            if (versionAttr != null)
                            {
                                versionAttr.Remove();
                                changed = true;
                            }
                        }

                        if (changed)
                        {
                            await fileService.WriteAllTextAsync(projectFile.Path, xDoc.ToString());
                            if (verbose)
                            {
                                AnsiConsole.MarkupLine($"[dim]Updated: {projectFile.Path}[/]");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine(
                            $"[red]Error updating {projectFile.Path}: {ex.Message}[/]"
                        );
                    }

                    task.Increment(1);
                }
            });

        AnsiConsole.MarkupLine("[green]Successfully updated all project files![/]");
    }
}
