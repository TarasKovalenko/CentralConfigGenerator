namespace CentralConfigGenerator.Core.NuGet;

public interface INuGetFeedService
{
    /// <summary>Fetches metadata for a single package, memoised for the lifetime of the service.</summary>
    Task<PackageMetadataSnapshot> GetMetadataAsync(
        string packageId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Fetches metadata for many packages with bounded parallelism.</summary>
    Task<IReadOnlyDictionary<string, PackageMetadataSnapshot>> GetMetadataAsync(
        IEnumerable<string> packageIds,
        int maxParallelism,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>The configured package sources, for diagnostics.</summary>
    IReadOnlyList<string> Sources { get; }
}
