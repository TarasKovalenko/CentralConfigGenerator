using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Services.Abstractions;

public interface IProjectFileService
{
    Task<IReadOnlyCollection<ProjectFile>> ScanDirectoryForProjectsAsync(DirectoryInfo directory);
}