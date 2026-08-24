using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Discovery;

public interface IProjectDiscoveryService
{
    Task<IReadOnlyList<ProjectFile>> DiscoverAsync(
        DiscoveryOptions options,
        CancellationToken cancellationToken = default
    );

    Task<ProjectFile?> LoadAsync(string path, CancellationToken cancellationToken = default);

    Task SaveAsync(ProjectFile projectFile, CancellationToken cancellationToken = default);
}
