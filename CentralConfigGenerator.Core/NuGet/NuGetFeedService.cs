using System.Collections.Concurrent;
using CentralConfigGenerator.Core.Analysis;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace CentralConfigGenerator.Core.NuGet;

/// <summary>
/// Talks to every package source configured for the workspace (nuget.config aware), collecting
/// version lists, deprecation notices, licence data and security advisories.
/// </summary>
public sealed class NuGetFeedService : INuGetFeedService, IDisposable
{
    private readonly List<SourceRepository> _repositories = [];
    private readonly SourceCacheContext _cache = new();
    private readonly ConcurrentDictionary<string, Task<PackageMetadataSnapshot>> _memo =
        new(StringComparer.OrdinalIgnoreCase);

    public NuGetFeedService(string? rootDirectory = null)
    {
        var sources = LoadSources(rootDirectory);

        foreach (var source in sources)
        {
            _repositories.Add(Repository.Factory.GetCoreV3(source));
        }

        if (_repositories.Count == 0)
        {
            _repositories.Add(
                Repository.Factory.GetCoreV3(
                    new PackageSource("https://api.nuget.org/v3/index.json")
                )
            );
        }

        Sources = _repositories.Select(r => r.PackageSource.Source).ToList();
    }

    public IReadOnlyList<string> Sources { get; }

    public Task<PackageMetadataSnapshot> GetMetadataAsync(
        string packageId,
        CancellationToken cancellationToken = default
    ) => _memo.GetOrAdd(packageId, id => FetchAsync(id, cancellationToken));

    public async Task<IReadOnlyDictionary<string, PackageMetadataSnapshot>> GetMetadataAsync(
        IEnumerable<string> packageIds,
        int maxParallelism,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var ids = packageIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new ConcurrentDictionary<string, PackageMetadataSnapshot>(
            StringComparer.OrdinalIgnoreCase
        );

        using var throttle = new SemaphoreSlim(Math.Max(1, maxParallelism));

        var tasks = ids.Select(async id =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                results[id] = await GetMetadataAsync(id, cancellationToken);
                progress?.Report(id);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    private async Task<PackageMetadataSnapshot> FetchAsync(
        string packageId,
        CancellationToken cancellationToken
    )
    {
        Exception? lastError = null;

        foreach (var repository in _repositories)
        {
            try
            {
                var resource = await repository.GetResourceAsync<PackageMetadataResource>(
                    cancellationToken
                )
                    ?? throw new InvalidOperationException(
                        $"Package source {repository.PackageSource.Source} does not expose package metadata."
                    );

                var metadata = (
                    await resource.GetMetadataAsync(
                        packageId,
                        includePrerelease: true,
                        includeUnlisted: false,
                        _cache,
                        NullLogger.Instance,
                        cancellationToken
                    )
                ).ToList();

                if (metadata.Count == 0)
                {
                    continue;
                }

                return await BuildSnapshotAsync(packageId, metadata, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
            }
        }

        return lastError is not null
            ? PackageMetadataSnapshot.Unavailable(packageId, lastError.Message)
            : new PackageMetadataSnapshot { PackageId = packageId, Found = false };
    }

    private static async Task<PackageMetadataSnapshot> BuildSnapshotAsync(
        string packageId,
        List<IPackageSearchMetadata> metadata,
        CancellationToken cancellationToken
    )
    {
        var ordered = metadata.OrderByDescending(m => m.Identity.Version).ToList();
        var newest = ordered[0];

        var deprecation = await SafeGetDeprecationAsync(newest);
        var vulnerabilities = new List<PackageVulnerability>();

        foreach (var entry in ordered)
        {
            if (entry.Vulnerabilities is null)
            {
                continue;
            }

            foreach (var vulnerability in entry.Vulnerabilities)
            {
                vulnerabilities.Add(
                    new PackageVulnerability
                    {
                        Severity = MapSeverity(vulnerability.Severity),
                        AdvisoryUrl = vulnerability.AdvisoryUrl?.ToString() ?? string.Empty,
                        AffectedRange = entry.Identity.Version.ToNormalizedString(),
                    }
                );
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new PackageMetadataSnapshot
        {
            PackageId = newest.Identity.Id,
            Found = true,
            AllVersions = ordered.Select(m => m.Identity.Version).ToList(),
            LatestStable = ordered.FirstOrDefault(m => !m.Identity.Version.IsPrerelease)?.Identity.Version,
            LatestIncludingPrerelease = newest.Identity.Version,
            IsDeprecated = deprecation is not null,
            DeprecationMessage = deprecation?.Message,
            DeprecationReasons = deprecation?.Reasons?.ToList() ?? [],
            AlternatePackageId = deprecation?.AlternatePackage?.PackageId,
            License = DescribeLicense(newest),
            LicenseUrl = newest.LicenseUrl?.ToString(),
            ProjectUrl = newest.ProjectUrl?.ToString(),
            Vulnerabilities = vulnerabilities,
        };
    }

    private static async Task<PackageDeprecationMetadata?> SafeGetDeprecationAsync(
        IPackageSearchMetadata metadata
    )
    {
        try
        {
            return await metadata.GetDeprecationMetadataAsync();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? DescribeLicense(IPackageSearchMetadata metadata)
    {
        if (metadata.LicenseMetadata is { } license)
        {
            return string.IsNullOrWhiteSpace(license.License)
                ? license.Type.ToString()
                : license.License;
        }

        return metadata.LicenseUrl?.ToString();
    }

    private static Severity MapSeverity(int severity) =>
        severity switch
        {
            >= 3 => Severity.Critical,
            2 => Severity.High,
            1 => Severity.Moderate,
            _ => Severity.Low,
        };

    private static List<PackageSource> LoadSources(string? rootDirectory)
    {
        try
        {
            var settings = Settings.LoadDefaultSettings(
                rootDirectory ?? Directory.GetCurrentDirectory()
            );
            var provider = new PackageSourceProvider(settings);
            return provider.LoadPackageSources().Where(s => s.IsEnabled).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public void Dispose() => _cache.Dispose();
}
