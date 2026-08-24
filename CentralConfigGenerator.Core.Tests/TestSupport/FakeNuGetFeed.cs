using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.NuGet;
using NuGet.Versioning;

namespace CentralConfigGenerator.Core.Tests.TestSupport;

/// <summary>A scripted feed so the network-backed rules can be tested deterministically.</summary>
public sealed class FakeNuGetFeed : INuGetFeedService
{
    private readonly Dictionary<string, PackageMetadataSnapshot> _snapshots =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Sources => ["https://fake/index.json"];

    public FakeNuGetFeed Add(
        string packageId,
        string[]? versions = null,
        bool deprecated = false,
        string? alternate = null,
        string? license = null,
        (string Version, Severity Severity)[]? vulnerabilities = null
    )
    {
        var parsed = (versions ?? ["1.0.0"]).Select(NuGetVersion.Parse).OrderByDescending(v => v).ToList();

        _snapshots[packageId] = new PackageMetadataSnapshot
        {
            PackageId = packageId,
            Found = true,
            AllVersions = parsed,
            LatestStable = parsed.FirstOrDefault(v => !v.IsPrerelease),
            LatestIncludingPrerelease = parsed.FirstOrDefault(),
            IsDeprecated = deprecated,
            DeprecationReasons = deprecated ? ["Legacy"] : [],
            AlternatePackageId = alternate,
            License = license,
            Vulnerabilities =
                vulnerabilities
                    ?.Select(v => new PackageVulnerability
                    {
                        Severity = v.Severity,
                        AdvisoryUrl = $"https://advisory/{packageId}",
                        AffectedRange = v.Version,
                    })
                    .ToList() ?? [],
        };

        return this;
    }

    public Task<PackageMetadataSnapshot> GetMetadataAsync(
        string packageId,
        CancellationToken cancellationToken = default
    ) =>
        Task.FromResult(
            _snapshots.TryGetValue(packageId, out var snapshot)
                ? snapshot
                : new PackageMetadataSnapshot { PackageId = packageId, Found = false }
        );

    public async Task<IReadOnlyDictionary<string, PackageMetadataSnapshot>> GetMetadataAsync(
        IEnumerable<string> packageIds,
        int maxParallelism,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = new Dictionary<string, PackageMetadataSnapshot>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in packageIds)
        {
            result[id] = await GetMetadataAsync(id, cancellationToken);
        }

        return result;
    }
}

/// <summary>A package graph service that returns whatever the test hands it.</summary>
public sealed class FakePackageGraphService(PackageGraph graph) : IPackageGraphService
{
    public Task<PackageGraph> LoadAsync(
        string target,
        string workingDirectory,
        bool includeTransitive,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(graph);
}
