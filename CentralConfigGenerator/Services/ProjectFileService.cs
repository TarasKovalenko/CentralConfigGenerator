using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Extensions;

namespace CentralConfigGenerator.Services;

public class ProjectFileService(
    IFileService fileService
) : IProjectFileService
{
    public async Task<IEnumerable<ProjectFile>> ScanDirectoryForProjectsAsync(DirectoryInfo directory)
    {
        MsgExtensions.LogInformation("Scanning directory for project files: {0}", directory.FullName);

        var projectFiles = new List<ProjectFile>();
        var csprojFiles = directory.GetFiles("*.csproj", SearchOption.AllDirectories);

        MsgExtensions.LogInformation("Found {0} .csproj files", csprojFiles.Length);

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
                MsgExtensions.LogError(ex, "Error reading project file: {0}", fullName);
            }
        }

        return projectFiles;
    }
}

public interface IProjectFileService
{
    Task<IEnumerable<ProjectFile>> ScanDirectoryForProjectsAsync(DirectoryInfo directory);
}