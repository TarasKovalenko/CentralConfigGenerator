using CentralConfigGenerator.Core.NuGet;
using CentralConfigGenerator.Core.Services;

namespace CentralConfigGenerator.Core.Workspace;

/// <summary>One package whose resolved version changed between two graph snapshots.</summary>
public sealed record GraphDrift
{
    public required string Key { get; init; }

    public required string PackageId { get; init; }

    public string? Before { get; init; }

    public string? After { get; init; }

    public bool IsAddition => Before is null;

    public bool IsRemoval => After is null;
}

public sealed record VerificationResult
{
    public required bool GraphAvailable { get; init; }

    public required IReadOnlyList<GraphDrift> Drift { get; init; }

    public string? Error { get; init; }

    public int BeforeCount { get; init; }

    public int AfterCount { get; init; }

    public bool IsClean => Drift.Count == 0;
}

public interface IVerificationService
{
    Task<PackageGraph> SnapshotAsync(
        string target,
        string workingDirectory,
        CancellationToken cancellationToken = default
    );

    VerificationResult Compare(PackageGraph before, PackageGraph after);
}

/// <summary>
/// Proves a migration did not change what NuGet actually resolves, by diffing the full package
/// graph before and after. This is the difference between "the files look right" and "the build
/// produces the same binaries".
/// </summary>
public sealed class VerificationService(
    IPackageGraphService graphService,
    IDotNetCliService cli
) : IVerificationService
{
    public async Task<PackageGraph> SnapshotAsync(
        string target,
        string workingDirectory,
        CancellationToken cancellationToken = default
    )
    {
        // dotnet list package needs an up-to-date assets file to report anything useful.
        var restore = await cli.RestoreAsync(target, workingDirectory, cancellationToken);
        if (!restore.Succeeded)
        {
            return PackageGraph.Empty with
            {
                Error = $"restore failed: {FirstError(restore.CombinedOutput)}",
            };
        }

        return await graphService.LoadAsync(
            target,
            workingDirectory,
            includeTransitive: true,
            cancellationToken
        );
    }

    public VerificationResult Compare(PackageGraph before, PackageGraph after)
    {
        if (!before.IsAvailable || !after.IsAvailable)
        {
            return new VerificationResult
            {
                GraphAvailable = false,
                Drift = [],
                Error = before.Error ?? after.Error,
                BeforeCount = before.Packages.Count,
                AfterCount = after.Packages.Count,
            };
        }

        var beforeSignature = before.Signature();
        var afterSignature = after.Signature();
        var drift = new List<GraphDrift>();

        foreach (var (key, version) in beforeSignature)
        {
            if (!afterSignature.TryGetValue(key, out var newVersion))
            {
                drift.Add(
                    new GraphDrift
                    {
                        Key = key,
                        PackageId = PackageIdFromKey(key),
                        Before = version,
                        After = null,
                    }
                );
                continue;
            }

            if (!string.Equals(version, newVersion, StringComparison.OrdinalIgnoreCase))
            {
                drift.Add(
                    new GraphDrift
                    {
                        Key = key,
                        PackageId = PackageIdFromKey(key),
                        Before = version,
                        After = newVersion,
                    }
                );
            }
        }

        foreach (var (key, version) in afterSignature)
        {
            if (!beforeSignature.ContainsKey(key))
            {
                drift.Add(
                    new GraphDrift
                    {
                        Key = key,
                        PackageId = PackageIdFromKey(key),
                        Before = null,
                        After = version,
                    }
                );
            }
        }

        return new VerificationResult
        {
            GraphAvailable = true,
            Drift = drift.OrderBy(d => d.PackageId, StringComparer.OrdinalIgnoreCase).ToList(),
            BeforeCount = beforeSignature.Count,
            AfterCount = afterSignature.Count,
        };
    }

    private static string PackageIdFromKey(string key)
    {
        var index = key.LastIndexOf('|');
        return index < 0 ? key : key[(index + 1)..];
    }

    private static string FirstError(string output) =>
        output
            .Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.Contains("error", StringComparison.OrdinalIgnoreCase))
        ?? "see build output";
}
