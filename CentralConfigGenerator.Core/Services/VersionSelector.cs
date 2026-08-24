using CentralConfigGenerator.Core.Services.Abstractions;
using NuGet.Versioning;

namespace CentralConfigGenerator.Core.Services;

/// <summary>
/// Chooses a single version from the set a package is referenced with, applying pre-release
/// filtering and the caller's conflict strategy.
/// </summary>
public sealed class VersionSelector(IVersionConflictResolver resolver)
{
    public string Select(
        string packageId,
        IReadOnlyList<string> versions,
        VersionResolutionStrategy strategy,
        bool ignorePrerelease,
        VersionComparison comparison = VersionComparison.VersionRelease
    )
    {
        var candidates = versions
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new ArgumentException(
                $"No versions supplied for package '{packageId}'.",
                nameof(versions)
            );
        }

        if (ignorePrerelease)
        {
            var stable = candidates.Where(v => !IsPrerelease(v)).ToList();

            // Only drop pre-releases when at least one stable version remains.
            if (stable.Count > 0)
            {
                candidates = stable;
            }
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        // Highest/Lowest are the only strategies where the comparison scope changes the answer:
        // it decides whether pre-release tags and build metadata participate in the ordering.
        if (strategy is VersionResolutionStrategy.Highest or VersionResolutionStrategy.Lowest)
        {
            var ordered = candidates
                .Where(v => NuGetVersion.TryParse(v, out _))
                .OrderBy(v => v, Comparer<string>.Create((a, b) => Compare(a, b, comparison)))
                .ToList();

            if (ordered.Count > 0)
            {
                return strategy == VersionResolutionStrategy.Highest ? ordered[^1] : ordered[0];
            }
        }

        return resolver.Resolve(packageId, candidates, strategy);
    }

    /// <summary>Compares two version strings using the requested NuGet comparison scope.</summary>
    public static int Compare(string left, string right, VersionComparison comparison)
    {
        var hasLeft = NuGetVersion.TryParse(left, out var leftVersion);
        var hasRight = NuGetVersion.TryParse(right, out var rightVersion);

        if (hasLeft && hasRight)
        {
            return new VersionComparer(comparison).Compare(leftVersion, rightVersion);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPrerelease(string version) =>
        NuGetVersion.TryParse(version, out var parsed) && parsed.IsPrerelease;

    public static bool IsFloating(string version) =>
        version.Contains('*', StringComparison.Ordinal)
        || (
            VersionRange.TryParse(version, out var range)
            && (range.IsFloating || !range.HasUpperBound && version.StartsWith('['))
        );
}
