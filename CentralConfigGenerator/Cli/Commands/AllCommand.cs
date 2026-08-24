using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Core.Models;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public sealed class AllSettings : MigrateSettings;

/// <summary>Generates both Directory.Build.props and Directory.Packages.props in one pass.</summary>
public sealed class AllCommand(MigrateCommand migrate) : AsyncCommand<AllSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AllSettings settings)
    {
        // The migration already knows how to hoist properties; --unify-props is what "all" means.
        var combined = settings;

        Ui.Quiet = settings.Quiet;
        Ui.Verbose = settings.Verbose;

        var migrateSettings = new MigrateSettings
        {
            Directory = combined.Directory,
            Solution = combined.Solution,
            Project = combined.Project,
            ExcludeDirs = combined.ExcludeDirs,
            Quiet = combined.Quiet,
            Verbose = combined.Verbose,
            NoConfig = combined.NoConfig,
            DryRun = combined.DryRun,
            Diff = combined.Diff,
            NoBackup = combined.NoBackup,
            BackupDir = combined.BackupDir,
            AddGitIgnore = combined.AddGitIgnore,
            Force = combined.Force,
            OutputDir = combined.OutputDir,
            Merge = combined.Merge,
            Overwrite = combined.Overwrite,
            KeepAttributes = combined.KeepAttributes,
            ConflictStrategy = combined.ConflictStrategy,
            InteractiveConflicts = combined.InteractiveConflicts,
            MinVersion = combined.MinVersion,
            IgnorePrerelease = combined.IgnorePrerelease,
            TransitivePinning = combined.TransitivePinning,
            Encoding = combined.Encoding,
            LineWrap = combined.LineWrap,
            VersionComparison = combined.VersionComparison,
            UnifyProps = true,
            IncludeFSharp = combined.IncludeFSharp,
        };

        return await migrate.ExecuteAsync(context, migrateSettings);
    }
}
