namespace CentralConfigGenerator.Core.Services;

/// <summary>
/// Thrown when projects disagree on a package version and the caller asked for the
/// <see cref="VersionResolutionStrategy.Fail"/> strategy.
/// </summary>
public sealed class VersionConflictException(string packageId, IReadOnlyList<string> versions)
    : Exception(
        $"Version conflict for '{packageId}': {string.Join(", ", versions)}. "
            + "Resolve it manually or choose a different conflict strategy."
    )
{
    public string PackageId { get; } = packageId;

    public IReadOnlyList<string> Versions { get; } = versions;
}
