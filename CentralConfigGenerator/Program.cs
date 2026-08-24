using CentralConfigGenerator.Cli.Commands;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Commands;
using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Analyzers;
using CentralConfigGenerator.Core.Analyzers.Abstractions;
using CentralConfigGenerator.Core.Configuration;
using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Core.Generators.Abstractions;
using CentralConfigGenerator.Core.IO;
using CentralConfigGenerator.Core.Migration;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.NuGet;
using CentralConfigGenerator.Core.Services;
using CentralConfigGenerator.Core.Services.Abstractions;
using CentralConfigGenerator.Core.Workspace;
using CentralConfigGenerator.Services;
using CentralConfigGenerator.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CentralConfigGenerator;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp(new TypeRegistrar(BuildServiceCollection()));

        app.Configure(config =>
        {
            config.SetApplicationName("central-config");
            config.SetApplicationVersion(ToolVersion);
            config.UseStrictParsing();
            config.CaseSensitivity(CaseSensitivity.None);

            config
                .AddCommand<Cli.Commands.MigrateCommand>("migrate")
                .WithAlias("packages")
                .WithAlias("convert")
                .WithAlias("packages-enhanced")
                .WithDescription("Migrate the workspace to central package management.")
                .WithExample("migrate", "--dry-run", "--diff")
                .WithExample("migrate", "-s", "MySolution.sln", "--transitive-pinning")
                .WithExample("migrate", "--merge", "--conflict-strategy", "Lowest");

            config
                .AddCommand<Cli.Commands.BuildPropsCommand>("build")
                .WithDescription("Hoist shared MSBuild properties into Directory.Build.props.")
                .WithExample("build", "--dry-run", "--diff");

            config
                .AddCommand<AllCommand>("all")
                .WithDescription("Generate Directory.Build.props and Directory.Packages.props together.")
                .WithExample("all", "--dry-run");

            config
                .AddCommand<RevertCommand>("revert")
                .WithDescription("Undo central package management, inlining versions again.")
                .WithExample("revert", "--dry-run", "--diff");

            config
                .AddCommand<AnalyzeCommand>("analyze")
                .WithAlias("check")
                .WithDescription("Score dependency health and report every problem found.")
                .WithExample("analyze", "--all-checks")
                .WithExample("analyze", "--audit", "--output", "Sarif", "--output-file", "report.sarif")
                .WithExample("analyze", "--fix");

            config
                .AddCommand<UpdateCommand>("update")
                .WithDescription("Update central versions, run the tests, roll back on failure.")
                .WithExample("update", "--dry-run")
                .WithExample("update", "--bisect");

            config
                .AddCommand<VerifyCommand>("verify")
                .WithDescription("Migrate, then prove the resolved package graph did not change.")
                .WithExample("verify", "-s", "MySolution.sln");

            config
                .AddCommand<TreeCommand>("tree")
                .WithDescription("Print the resolved dependency graph.")
                .WithExample("tree", "--transitive");

            config
                .AddCommand<StatusCommand>("status")
                .WithDescription("One-shot dashboard of the workspace and what to do next.");

            config
                .AddCommand<DoctorCommand>("doctor")
                .WithDescription("Diagnose the environment before anything else goes wrong.");

            config
                .AddCommand<InitCommand>("init")
                .WithDescription("Scaffold a .centralconfig.json.");

            config
                .AddCommand<ExplainCommand>("explain")
                .WithDescription("Explain an analysis rule, or list them all.")
                .WithExample("explain", "all")
                .WithExample("explain", "SecurityVulnerability");

            config
                .AddCommand<BatchCommand>("batch")
                .WithDescription("Run the migration across every solution in a monorepo.")
                .WithExample("batch", "./repos", "--parallel", "--continue");

            config.AddBranch(
                "backups",
                branch =>
                {
                    branch.SetDescription("Inspect, restore and prune rollback backups.");
                    branch.AddCommand<BackupListCommand>("list").WithDescription("List every backup.");
                    branch
                        .AddCommand<BackupRestoreCommand>("restore")
                        .WithDescription("Restore a backup, undoing the change that created it.");
                    branch.AddCommand<BackupPruneCommand>("prune").WithDescription("Delete old backups.");
                }
            );

            config
                .AddCommand<CompletionsCommand>("completions")
                .WithDescription("Emit a shell completion script (bash, zsh, fish, powershell).");

            config.SetExceptionHandler(
                (exception, _) =>
                {
                    if (exception is CommandParseException or CommandRuntimeException)
                    {
                        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(exception.Message)}");
                        AnsiConsole.MarkupLine("[grey]Run with --help to see the available options.[/]");
                        return ExitCode.ValidationError;
                    }

                    if (exception is VersionConflictException conflict)
                    {
                        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(conflict.Message)}");
                        return ExitCode.VersionConflict;
                    }

                    if (exception is UnauthorizedAccessException or IOException)
                    {
                        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(exception.Message)}");
                        return ExitCode.FileOperationError;
                    }

                    AnsiConsole.MarkupLine($"[red]✗ Unexpected error:[/] {Markup.Escape(exception.Message)}");

                    if (Ui.Verbose)
                    {
                        AnsiConsole.WriteException(exception, ExceptionFormats.ShortenEverything);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[grey]Re-run with --verbose for the full stack trace.[/]");
                    }

                    return ExitCode.UnexpectedError;
                }
            );
        });

        return await app.RunAsync(args);
    }

    private static string ToolVersion =>
        typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "2.0.0";

    /// <summary>Registers every service the CLI needs. Public so tests can assert the graph resolves.</summary>
    public static ServiceProvider ConfigureServices() =>
        BuildServiceCollection().BuildServiceProvider();

    internal static IServiceCollection BuildServiceCollection()
    {
        var services = new ServiceCollection();

        // Infrastructure
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IProjectDiscoveryService, ProjectDiscoveryService>();
        services.AddSingleton<IDotNetCliService, DotNetCliService>();
        services.AddSingleton<IPackageGraphService, PackageGraphService>();
        services.AddSingleton<INuGetFeedService>(_ => new NuGetFeedService());
        services.AddSingleton<ConfigurationLoader>();
        services.AddScoped<EffectiveSettings>();

        // Migration
        services.AddSingleton<IVersionConflictResolver, VersionConflictResolver>();
        services.AddSingleton<VersionSelector>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IMigrationService, MigrationService>();

        // Analysis
        services.AddSingleton<IBaselineService, BaselineService>();
        services.AddSingleton<IAnalysisEngine, AnalysisEngine>();
        services.AddSingleton<IAutoFixService, AutoFixService>();

        // Workspace tooling
        services.AddSingleton<IVerificationService, VerificationService>();
        services.AddSingleton<IPackageUpdateService, PackageUpdateService>();
        services.AddSingleton<DependencyTreeService>();
        services.AddSingleton<DoctorService>();
        services.AddSingleton<StatusService>();
        services.AddSingleton<BatchService>();

        // CLI commands
        services.AddTransient<Cli.Commands.MigrateCommand>();
        services.AddTransient<Cli.Commands.BuildPropsCommand>();
        services.AddTransient<AllCommand>();
        services.AddTransient<RevertCommand>();
        services.AddTransient<AnalyzeCommand>();
        services.AddTransient<UpdateCommand>();
        services.AddTransient<VerifyCommand>();
        services.AddTransient<TreeCommand>();
        services.AddTransient<StatusCommand>();
        services.AddTransient<DoctorCommand>();
        services.AddTransient<InitCommand>();
        services.AddTransient<ExplainCommand>();
        services.AddTransient<BatchCommand>();
        services.AddTransient<BackupListCommand>();
        services.AddTransient<BackupRestoreCommand>();
        services.AddTransient<BackupPruneCommand>();
        services.AddTransient<CompletionsCommand>();

        RegisterLegacyServices(services);

        return services;
    }

    /// <summary>
    /// The original analyzer/generator pipeline. Still registered so the public API stays stable
    /// for anyone consuming CentralConfigGenerator.Core directly.
    /// </summary>
    private static void RegisterLegacyServices(IServiceCollection services)
    {
        services.AddSingleton<IProjectAnalyzer, ProjectAnalyzer>();
        services.AddSingleton<IPackageAnalyzer, PackageAnalyzer>();
        services.AddSingleton<IBuildPropsGenerator, BuildPropsGenerator>();
        services.AddSingleton<IPackagesPropsGenerator, PackagesPropsGenerator>();
        services.AddSingleton<IEnhancedPackageAnalyzer, EnhancedPackageAnalyzer>();
        services.AddSingleton<IVersionConflictVisualizer, VersionConflictVisualizer>();
        services.AddSingleton<IVersionCompatibilityChecker, VersionCompatibilityChecker>();

        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IProjectFileService, ProjectFileService>();

        services.AddTransient<Commands.BuildPropsCommand>();
        services.AddTransient<PackagesPropsCommand>();
        services.AddTransient<EnhancedPackagesPropsCommand>();
    }
}
