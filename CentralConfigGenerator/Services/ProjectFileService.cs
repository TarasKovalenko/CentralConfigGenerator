using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Extensions;

namespace CentralConfigGenerator.Services;

public class ProjectFileService(
    IFileService fileService
) : IProjectFileService
{
    public async Task<IReadOnlyCollection<ProjectFile>> ScanDirectoryForProjectsAsync(DirectoryInfo directory)
    {
        MsgLogger.LogInformation("Scanning directory for project files: {0}", directory.FullName);

        var projectFiles = new List<ProjectFile>();
        var csprojFiles = directory.GetFiles("*.csproj", SearchOption.AllDirectories);

        MsgLogger.LogInformation("Found {0} .csproj files", csprojFiles.Length);

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
                MsgLogger.LogError(ex, "Error reading project file: {0}", fullName);
            }
        }

        return projectFiles;
    }
}

public interface IProjectFileService
{
    Task<IReadOnlyCollection<ProjectFile>> ScanDirectoryForProjectsAsync(DirectoryInfo directory);
}