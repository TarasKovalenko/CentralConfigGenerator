using NuGet.Versioning;

namespace CentralConfigGenerator.Core.Services;

public interface IVersionConflictResolver
{
    string Resolve(string packageName, IEnumerable<string> versions, VersionResolutionStrategy strategy);
}

public enum VersionResolutionStrategy
{
    Highest,
    Lowest,
    MostCommon,
    Manual
}

public record ParsedVersion
{
    public required string Original { get; init; }
    public NuGetVersion? Parsed { get; init; }
    public VersionRange? Range { get; init; }
}

public class VersionConflictResolver : IVersionConflictResolver
{
    public string Resolve(string packageName, IEnumerable<string> versions, VersionResolutionStrategy strategy)
    {
        var versionList = versions.ToList();

        if (versionList.Count == 0)
        {
            throw new ArgumentException("No versions provided for resolution");
        }

        if (versionList.Count == 1)
        {
            return versionList[0];
        }

        // Parse all versions into NuGetVersion objects where possible
        var parsedVersions = versionList
            .Select(v => new ParsedVersion
            {
                Original = v,
                Parsed = NuGetVersion.TryParse(v, out var parsed) ? parsed : null,
                Range = VersionRange.TryParse(v, out var range) ? range : null
            })
            .ToList();

        return strategy switch
        {
            VersionResolutionStrategy.Highest => ResolveHighest(parsedVersions),
            VersionResolutionStrategy.Lowest => ResolveLowest(parsedVersions),
            VersionResolutionStrategy.MostCommon => ResolveMostCommon(versionList),
            VersionResolutionStrategy.Manual => throw new InvalidOperationException(
                $"Manual resolution required for package '{packageName}'. Versions found: {string.Join(", ", versionList)}"),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy))
        };
    }

    private static string ResolveHighest(List<ParsedVersion> parsedVersions)
    {
        // First, try to find the highest parsed version
        var highestParsed = parsedVersions
            .Where(v => v.Parsed != null)
            .OrderByDescending(v => v.Parsed)
            .FirstOrDefault();

        if (highestParsed != null)
        {
            return highestParsed.Original;
        }

        // If no parsed versions, try version ranges
        var versionRanges = parsedVersions
            .Where(v => v.Range is { HasLowerBound: true })
            .OrderByDescending(v => v.Range!.MinVersion)
            .FirstOrDefault();

        if (versionRanges != null)
        {
            return versionRanges.Original;
        }

        // Fallback to string comparison
        return parsedVersions
            .OrderByDescending(v => v.Original)
            .First()
            .Original;
    }

    private static string ResolveLowest(List<ParsedVersion> parsedVersions)
    {
        // Try to find the lowest parsed version
        var lowestParsed = parsedVersions
            .Where(v => v.Parsed != null)
            .OrderBy(v => v.Parsed)
            .FirstOrDefault();

        if (lowestParsed != null)
        {
            return lowestParsed.Original;
        }

        // If no parsed versions, try version ranges
        var versionRanges = parsedVersions
            .Where(v => v.Range is { HasLowerBound: true })
            .OrderBy(v => v.Range!.MinVersion)
            .FirstOrDefault();

        if (versionRanges != null)
        {
            return versionRanges.Original;
        }

        // Fallback to string comparison
        return parsedVersions
            .OrderBy(v => v.Original)
            .First()
            .Original;
    }

    private static string ResolveMostCommon(List<string> versions)
    {
        return versions
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First()
            .Key;
    }
}