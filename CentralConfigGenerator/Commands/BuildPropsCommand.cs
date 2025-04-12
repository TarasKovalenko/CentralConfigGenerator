using System.Xml.Linq;
using CentralConfigGenerator.Core.Analyzers;
using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Services;
using Microsoft.Extensions.Logging;

namespace CentralConfigGenerator.Commands;

public class BuildPropsCommand(
    ILogger<BuildPropsCommand> logger,
    IProjectAnalyzer projectAnalyzer,
    IBuildPropsGenerator buildPropsGenerator,
    IFileService fileService
)
{
    public async Task ExecuteAsync(DirectoryInfo directory, bool overwrite)
    {
        logger.LogWarning("Generating Directory.Build.props for directory: {Directory}", directory.FullName);

        var targetPath = Path.Combine(directory.FullName, "Directory.Build.props");

        // Check if file already exists
        if (fileService.Exists(targetPath) && !overwrite)
        {
            logger.LogWarning("File Directory.Build.props already exists. Use --overwrite to replace it.");
            return;
        }

        // Scan for project files
        var projectFiles = new List<ProjectFile>();
        foreach (var fullName in directory.GetFiles("*.csproj", SearchOption.AllDirectories).Select(f => f.FullName))
        {
            try
            {
                var content = await fileService.ReadAllTextAsync(fullName);
                projectFiles.Add(new ProjectFile
                {
                    Path = fullName,
                    Content = content
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading project file: {FilePath}", fullName);
            }
        }

        if (projectFiles.Count == 0)
        {
            logger.LogWarning("No .csproj files found in the directory tree.");
            return;
        }

        logger.LogInformation("Found {Count} project files", projectFiles.Count);

        // Extract common properties
        var commonProperties = projectAnalyzer.ExtractCommonProperties(projectFiles);
        logger.LogInformation("Identified {Count} common properties", commonProperties.Count);

        foreach (var prop in commonProperties)
        {
            logger.LogDebug("Common property: {PropertyName} = {PropertyValue}", prop.Key, prop.Value);
        }

        // Generate build props content
        var buildPropsContent = buildPropsGenerator.GenerateBuildPropsContent(commonProperties);

        // Write to file
        await fileService.WriteAllTextAsync(targetPath, buildPropsContent);

        logger.LogInformation("Created Directory.Build.props at {FilePath}", targetPath);

        logger.LogInformation("Removing centralized properties from project files...");

        foreach (var projectFile in projectFiles)
        {
            try
            {
                // Load the project file
                var xDoc = XDocument.Parse(projectFile.Content);
                var changed = false;

                // Find properties that are now in Directory.Build.props
                foreach (var property in commonProperties.Keys)
                {
                    var elements = xDoc.Descendants(property).ToList();
                    foreach (var element in elements)
                    {
                        // Remove the property from the project file
                        element.Remove();
                        changed = true;
                    }
                }

                // Save the modified project file if changes were made
                if (changed)
                {
                    await fileService.WriteAllTextAsync(projectFile.Path, xDoc.ToString());
                    logger.LogInformation("Updated project file: {FilePath}", projectFile.Path);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating project file: {FilePath}", projectFile.Path);
            }
        }
    }
}