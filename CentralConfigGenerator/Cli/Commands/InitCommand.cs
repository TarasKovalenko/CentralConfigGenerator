using System.ComponentModel;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Cli.Rendering;
using CentralConfigGenerator.Core.Configuration;
using CentralConfigGenerator.Core.Models;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public sealed class InitSettings : CommandSettings
{
    [CommandOption("-d|--directory <PATH>")]
    [Description("Where the config file is written. Defaults to the current directory.")]
    public string? Directory { get; init; }

    [CommandOption("-f|--force")]
    [Description("Overwrite an existing .centralconfig.json.")]
    public bool Force { get; init; }

    [CommandOption("-q|--quiet")]
    public bool Quiet { get; init; }
}

/// <summary>Scaffolds a .centralconfig.json so the team shares one set of defaults.</summary>
public sealed class InitCommand(ConfigurationLoader loader) : AsyncCommand<InitSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, InitSettings settings, CancellationToken cancellationToken)
    {
        Ui.Quiet = settings.Quiet;

        var directory = Path.GetFullPath(settings.Directory ?? System.IO.Directory.GetCurrentDirectory());
        var path = Path.Combine(directory, ConfigurationLoader.FileName);

        if (File.Exists(path) && !settings.Force)
        {
            Ui.Warn($"{ConfigurationLoader.FileName} already exists. Use --force to overwrite.");
            return ExitCode.ValidationError;
        }

        var written = await loader.ScaffoldAsync(directory, settings.Force);

        Ui.Success($"Created {ReportRenderer.Relative(written)}.");
        Ui.Info("[grey]Edit it to set your conflict strategy, rule severities and backup retention.[/]");

        return ExitCode.Success;
    }
}
