using System.CommandLine;
using CentralConfigGenerator.Commands;
using CentralConfigGenerator.Core.Analyzers;
using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CentralConfigGenerator;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        // Setup dependency injection
        var services = ConfigureServices();

        // Create root command
        var rootCommand = new RootCommand
        {
            Description = "A tool to generate centralized configuration files for .NET projects"
        };

        // Create commands
        var buildCommand = new Command("build", "Generate Directory.Build.props file");
        var packagesCommand = new Command("packages", "Generate Directory.Packages.props file");
        var allCommand = new Command("all", "Generate both Directory.Build.props and Directory.Packages.props files");

        // Create common options
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

        // Add options to commands
        buildCommand.AddOption(directoryOption);
        buildCommand.AddOption(overwriteOption);
        buildCommand.AddOption(verboseOption);

        packagesCommand.AddOption(directoryOption);
        packagesCommand.AddOption(overwriteOption);
        packagesCommand.AddOption(verboseOption);

        allCommand.AddOption(directoryOption);
        allCommand.AddOption(overwriteOption);
        allCommand.AddOption(verboseOption);

        // Set command handlers
        buildCommand.SetHandler(async (directory, overwrite, verbose) =>
        {
            var command = services.GetRequiredService<BuildPropsCommand>();
            await command.ExecuteAsync(directory, overwrite);
        }, directoryOption, overwriteOption, verboseOption);

        packagesCommand.SetHandler(async (directory, overwrite, verbose) =>
        {
            var command = services.GetRequiredService<PackagesPropsCommand>();
            await command.ExecuteAsync(directory, overwrite);
        }, directoryOption, overwriteOption, verboseOption);

        allCommand.SetHandler(async (directory, overwrite, verbose) =>
        {
            var buildCommand = services.GetRequiredService<BuildPropsCommand>();
            var packagesCommand = services.GetRequiredService<PackagesPropsCommand>();

            await buildCommand.ExecuteAsync(directory, overwrite);
            await packagesCommand.ExecuteAsync(directory, overwrite);
        }, directoryOption, overwriteOption, verboseOption);

        // Add commands to root
        rootCommand.AddCommand(buildCommand);
        rootCommand.AddCommand(packagesCommand);
        rootCommand.AddCommand(allCommand);

        // Run the command
        return await rootCommand.InvokeAsync(args);
    }

    static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register core services
        services.AddSingleton<IProjectAnalyzer, ProjectAnalyzer>();
        services.AddSingleton<IPackageAnalyzer, PackageAnalyzer>();
        services.AddSingleton<IBuildPropsGenerator, BuildPropsGenerator>();
        services.AddSingleton<IPackagesPropsGenerator, PackagesPropsGenerator>();

        // Register commands
        services.AddTransient<BuildPropsCommand>();
        services.AddTransient<PackagesPropsCommand>();

        // Register file service
        services.AddSingleton<IFileService, FileService>();

        return services.BuildServiceProvider();
    }
}