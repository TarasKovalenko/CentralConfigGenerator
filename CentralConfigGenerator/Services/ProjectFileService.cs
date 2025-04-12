using CentralConfigGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace CentralConfigGenerator.Services;

public class ProjectFileService(
    IFileService fileService,
    ILogger<ProjectFileService> logger
) : IProjectFileService
{
    public async Task<IEnumerable<ProjectFile>> ScanDirectoryForProjectsAsync(DirectoryInfo directory)
    {
        logger.LogInformation("Scanning directory for project files: {Directory}", directory.FullName);

        var projectFiles = new List<ProjectFile>();
        var csprojFiles = directory.GetFiles("*.csproj", SearchOption.AllDirectories);

        logger.LogInformation("Found {Count} .csproj files", csprojFiles.Length);

        foreach (var fullName in csprojFiles.Select(f => f.FullName))
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

        return projectFiles;
    }
}

public interface IProjectFileService
{
    Task<IEnumerable<ProjectFile>> ScanDirectoryForProjectsAsync(DirectoryInfo directory);
}