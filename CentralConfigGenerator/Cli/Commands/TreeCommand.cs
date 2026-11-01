using System.ComponentModel;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Workspace;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public sealed class TreeSettings : WorkspaceSettings
{
    [CommandOption("--transitive")]
    [Description("Include transitive dependencies.")]
    public bool Transitive { get; init; }
}

/// <summary>Prints the resolved dependency graph as an ASCII tree.</summary>
public sealed class TreeCommand(DependencyTreeService treeService) : AsyncCommand<TreeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, TreeSettings settings, CancellationToken cancellationToken)
    {
        Ui.Quiet = settings.Quiet;
        Ui.Verbose = settings.Verbose;

        var target = settings.Solution ?? settings.Project ?? settings.ResolvedDirectory;

        var output = await Ui.StatusAsync(
            "Resolving the dependency graph…",
            async _ =>
                await treeService.RenderAsync(target, settings.ResolvedDirectory, settings.Transitive)
        );

        if (output.StartsWith("Dependency graph unavailable", StringComparison.Ordinal))
        {
            Ui.Error(output);
            Ui.Info("[grey]Run 'dotnet restore' first, or check that the dotnet CLI is on PATH.[/]");
            return ExitCode.IncompleteAnalysis;
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            Ui.Warn("No packages were resolved.");
            return ExitCode.NoProjectsFound;
        }

        Console.Out.Write(output);
        return ExitCode.Success;
    }
}
