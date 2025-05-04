using System.Xml.Linq;
using CentralConfigGenerator.Core.Analyzers;
using CentralConfigGenerator.Core.Analyzers.Abstractions;
using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Core.Generators.Abstractions;
using CentralConfigGenerator.Extensions;
using CentralConfigGenerator.Services.Abstractions;

namespace CentralConfigGenerator.Commands;

public class BuildPropsCommand(
    IProjectAnalyzer projectAnalyzer,
    IProjectFileService projectFileService,
    IBuildPropsGenerator buildPropsGenerator,
    IFileService fileService
)
{
    public async Task ExecuteAsync(DirectoryInfo directory, bool overwrite)
    {
        MsgLogger.LogInformation("Generating Directory.Build.props for directory: {0}", directory.FullName);

        var targetPath = Path.Combine(directory.FullName, "Directory.Build.props");

        if (fileService.Exists(targetPath) && !overwrite)
        {
            MsgLogger.LogWarning("File Directory.Build.props already exists. Use --overwrite to replace it.");
            return;
        }

        var projectFiles = await projectFileService.ScanDirectoryForProjectsAsync(directory);

        if (projectFiles.Count == 0)
        {
            MsgLogger.LogWarning("No .csproj files found in the directory tree.");
            return;
        }

        MsgLogger.LogInformation("Found {0} project files", projectFiles.Count);

        var commonProperties = projectAnalyzer.ExtractCommonProperties(projectFiles);
        MsgLogger.LogInformation("Identified {0} common properties", commonProperties.Count);

        foreach (var prop in commonProperties)
        {
            MsgLogger.LogDebug("Common property: {0} = {1}", prop.Key, prop.Value);
        }

        var buildPropsContent = buildPropsGenerator.GenerateBuildPropsContent(commonProperties);

        await fileService.WriteAllTextAsync(targetPath, buildPropsContent);

        MsgLogger.LogInformation("Created Directory.Build.props at {0}", targetPath);

        MsgLogger.LogInformation("Removing centralized properties from project files...");

        // Define the required properties we want to remove from individual projects
        var requiredPropertyNames = new[] { "TargetFramework", "ImplicitUsings", "Nullable" };

        foreach (var projectFile in projectFiles)
        {
            try
            {
                var xDoc = XDocument.Parse(projectFile.Content);
                var changed = false;

                foreach (var property in requiredPropertyNames)
                {
                    // Find all property elements with the specified name
                    var propertyElements = xDoc.Descendants(property).ToList();
                    
                    foreach (var element in propertyElements)
                    {
                        element.Remove();
                        changed = true;
                    }
                }

                if (changed)
                {
                    await fileService.WriteAllTextAsync(projectFile.Path, xDoc.ToString());
                    MsgLogger.LogInformation("Updated project file: {0}", projectFile.Path);
                }
            }
            catch (Exception ex)
            {
                MsgLogger.LogError(ex, "Error updating project file: {0}", projectFile.Path);
            }
        }
    }
}