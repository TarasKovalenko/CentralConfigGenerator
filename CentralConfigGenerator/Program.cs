using System.CommandLine;
using CentralConfigGenerator.Commands;
using CentralConfigGenerator.Core.Analyzers;
using CentralConfigGenerator.Core.Analyzers.Abstractions;
using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Core.Generators.Abstractions;
using CentralConfigGenerator.Core.Services;
using CentralConfigGenerator.Core.Services.Abstractions;
using CentralConfigGenerator.Services;
using CentralConfigGenerator.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CentralConfigGenerator;

public class Program
{
    static async Task<int> Main(string[] args)
    {
        var services = ConfigureServices();

        var rootCommand = new RootCommand
        {
            Description = "A tool to generate centralized configuration files for .NET projects"
        };

        var buildCommand = new Command("build", "Generate Directory.Build.props file");
        var packagesCommand = new Command("packages", "Generate Directory.Packages.props file");
        var packagesEnhancedCommand = new Command("packages-enhanced", "Generate Directory.Packages.props file with enhanced version analysis");
        var allCommand = new Command("all", "Generate both Directory.Build.props and Directory.Packages.props files");

        var directoryOption = new Option<DirectoryInfo>(
            ["--directory", "-d"],
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "The directory to scan for .NET projects"
        );

        var overwriteOption = new Option<bool>(
            ["--overwrite", "-o"],
            () => false,
            "Overwrite existing files"
        );

        var verboseOption = new Option<bool>(
            ["--verbose", "-v"],
            () => false,
            "Enable verbose logging"
        );

        buildCommand.AddOption(directoryOption);
        buildCommand.AddOption(overwriteOption);
        buildCommand.AddOption(verboseOption);

        packagesCommand.AddOption(directoryOption);
        packagesCommand.AddOption(overwriteOption);
        packagesCommand.AddOption(verboseOption);

        packagesEnhancedCommand.AddOption(directoryOption);
        packagesEnhancedCommand.AddOption(overwriteOption);
        packagesEnhancedCommand.AddOption(verboseOption);

        allCommand.AddOption(directoryOption);
        allCommand.AddOption(overwriteOption);
        allCommand.AddOption(verboseOption);

        buildCommand.SetHandler(async (directory, overwrite, _) =>
        {
            var command = services.GetRequiredService<BuildPropsCommand>();
            ArgumentNullException.ThrowIfNull(command);

            await command.ExecuteAsync(directory, overwrite);
        }, directoryOption, overwriteOption, verboseOption);

        packagesCommand.SetHandler(async (directory, overwrite, _) =>
        {
            var command = services.GetRequiredService<PackagesPropsCommand>();
            ArgumentNullException.ThrowIfNull(command);

            await command.ExecuteAsync(directory, overwrite);
        }, directoryOption, overwriteOption, verboseOption);

        packagesEnhancedCommand.SetHandler(async (directory, overwrite, verbose) =>
        {
            var command = services.GetRequiredService<EnhancedPackagesPropsCommand>();
            ArgumentNullException.ThrowIfNull(command);

            await command.ExecuteAsync(directory, overwrite, verbose);
        }, directoryOption, overwriteOption, verboseOption);

        allCommand.SetHandler(async (directory, overwrite, _) =>
        {
            var buildPropsCommand = services.GetRequiredService<BuildPropsCommand>();
            ArgumentNullException.ThrowIfNull(buildPropsCommand);

            var packagesPropsCommand = services.GetRequiredService<PackagesPropsCommand>();
            ArgumentNullException.ThrowIfNull(packagesPropsCommand);

            await buildPropsCommand.ExecuteAsync(directory, overwrite);
            await packagesPropsCommand.ExecuteAsync(directory, overwrite);
        }, directoryOption, overwriteOption, verboseOption);

        rootCommand.AddCommand(buildCommand);
        rootCommand.AddCommand(packagesCommand);
        rootCommand.AddCommand(packagesEnhancedCommand);
        rootCommand.AddCommand(allCommand);

        return await rootCommand.InvokeAsync(args);
    }

    public static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Original services
        services.AddSingleton<IProjectAnalyzer, ProjectAnalyzer>();
        services.AddSingleton<IPackageAnalyzer, PackageAnalyzer>();
        services.AddSingleton<IBuildPropsGenerator, BuildPropsGenerator>();
        services.AddSingleton<IPackagesPropsGenerator, PackagesPropsGenerator>();

        // Enhanced services
        services.AddSingleton<IVersionConflictResolver, VersionConflictResolver>();
        services.AddSingleton<IEnhancedPackageAnalyzer, EnhancedPackageAnalyzer>();
        services.AddSingleton<IVersionConflictVisualizer, VersionConflictVisualizer>();
        services.AddSingleton<IVersionCompatibilityChecker, VersionCompatibilityChecker>();

        // Commands
        services.AddTransient<BuildPropsCommand>();
        services.AddTransient<PackagesPropsCommand>();
        services.AddTransient<EnhancedPackagesPropsCommand>();

        // Common services
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IProjectFileService, ProjectFileService>();

        return services.BuildServiceProvider();
    }
}
