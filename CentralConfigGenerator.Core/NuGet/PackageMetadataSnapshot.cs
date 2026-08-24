using CentralConfigGenerator.Core.Analysis;
using NuGetVersioning = NuGet.Versioning;

namespace CentralConfigGenerator.Core.NuGet;

/// <summary>Everything the analyzers need to know about a package, fetched once per run.</summary>
public sealed record PackageMetadataSnapshot
{
    public required string PackageId { get; init; }

    public bool Found { get; init; }

    public IReadOnlyList<NuGetVersioning.NuGetVersion> AllVersions { get; init; } = [];

    public NuGetVersioning.NuGetVersion? LatestStable { get; init; }

    public NuGetVersioning.NuGetVersion? LatestIncludingPrerelease { get; init; }

    public bool IsDeprecated { get; init; }

    public string? DeprecationMessage { get; init; }

    public IReadOnlyList<string> DeprecationReasons { get; init; } = [];

    public string? AlternatePackageId { get; init; }

    public string? License { get; init; }

    public string? LicenseUrl { get; init; }

    public string? ProjectUrl { get; init; }

    public IReadOnlyList<PackageVulnerability> Vulnerabilities { get; init; } = [];

    /// <summary>Set when the feed could not be reached; the run is then reported as incomplete.</summary>
    public string? Error { get; init; }

    public static PackageMetadataSnapshot Unavailable(string packageId, string error) =>
        new()
        {
            PackageId = packageId,
            Found = false,
            Error = error,
        };
}

public sealed record PackageVulnerability
{
    public required Severity Severity { get; init; }

    public required string AdvisoryUrl { get; init; }

    /// <summary>Version range the advisory applies to, when the feed reports one.</summary>
    public string? AffectedRange { get; init; }
}
