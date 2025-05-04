using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Services.Abstractions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace CentralConfigGenerator.Core.Services;

public class VersionCompatibilityChecker : IVersionCompatibilityChecker
{
    private readonly SourceRepository _repository;

    public VersionCompatibilityChecker()
    {
        var providers = new List<Lazy<INuGetResourceProvider>>(Repository.Provider.GetCoreV3());
        _repository = new SourceRepository(
            new PackageSource("https://api.nuget.org/v3/index.json"),
            providers
        );
    }

    public async Task<CompatibilityCheckResult> CheckCompatibilityAsync(
        string packageId,
        string version
    )
    {
        var result = new CompatibilityCheckResult();
        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            result.IsCompatible = false;
            result.Issues.Add($"Invalid version format: {version}");
            return result;
        }

        // NuGet API lookup
        try
        {
            var metadataResource = await _repository.GetResourceAsync<PackageMetadataResource>();
            var searchMetadata = await metadataResource.GetMetadataAsync(
                packageId,
                includePrerelease: true,
                includeUnlisted: false,
                new SourceCacheContext(),
                NullLogger.Instance,
                CancellationToken.None
            );

            var allVersions = searchMetadata
                .Select(m => m.Identity.Version)
                .OrderByDescending(v => v)
                .ToList();

            if (allVersions.Count == 0)
            {
                result.Issues.Add("Package not found in NuGet repository.");
                result.IsCompatible = false;
                return result;
            }

            // Check for pre-release
            if (nugetVersion.IsPrerelease)
            {
                result.Issues.Add(
                    "Pre-release version detected. Consider using a stable release for production."
                );
            }

            // Check outdated major version
            var latestStable = allVersions.FirstOrDefault(v => !v.IsPrerelease);
            if (latestStable != null && nugetVersion.Major < latestStable.Major - 1)
            {
                result.Issues.Add(
                    $"This version is significantly outdated. Latest stable is {latestStable}."
                );
                result.SuggestedVersion ??= latestStable.ToNormalizedString();
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Failed to fetch package metadata: {ex.Message}");
        }

        return result;
    }
}
