using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Core.IO;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.NuGet;
using CentralConfigGenerator.Core.Services;
using CentralConfigGenerator.Core.Services.Abstractions;
using NuGet.Versioning;

namespace CentralConfigGenerator.Core.Workspace;

public sealed record PackageUpdateOptions
{
    public string RootDirectory { get; init; } = Directory.GetCurrentDirectory();

    public string? SolutionPath { get; init; }

    public string? PackagesPropsPath { get; init; }

    public bool DryRun { get; init; }

    public bool IncludePrerelease { get; init; }

    /// <summary>Keep the largest subset of updates that still passes tests instead of reverting all.</summary>
    public bool Bisect { get; init; }

    /// <summary>Upper bound on restore + test cycles spent bisecting.</summary>
    public int BisectBudget { get; init; } = 16;

    public string? TestFilter { get; init; }

    /// <summary>Restrict the run to these package ids.</summary>
    public IReadOnlyCollection<string> Only { get; init; } = [];

    public bool CreateBackup { get; init; } = true;

    public string? BackupDirectory { get; init; }

    /// <summary>Skip the test run entirely and just write the updates.</summary>
    public bool SkipTests { get; init; }

    public int MaxParallelism { get; init; } = 8;
}

public sealed record PackageUpdateCandidate
{
    public required string PackageId { get; init; }

    public required string CurrentVersion { get; init; }

    public required string NewVersion { get; init; }

    public bool IsMajorUpgrade { get; init; }
}

public sealed record PackageUpdateResult
{
    public required IReadOnlyList<PackageUpdateCandidate> Candidates { get; init; }

    public required IReadOnlyList<PackageUpdateCandidate> Applied { get; init; }

    public required IReadOnlyList<PackageUpdateCandidate> HeldBack { get; init; }

    public bool TestsRun { get; init; }

    public bool TestsPassed { get; init; }

    public int BisectCycles { get; init; }

    public string? Message { get; init; }

    public BackupSet? Backup { get; init; }

    public bool RolledBack { get; init; }
}

public interface IPackageUpdateService
{
    Task<PackageUpdateResult> UpdateAsync(
        PackageUpdateOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Updates central package versions, runs the test suite, and rolls back on failure. With
/// <c>--bisect</c> it keeps the largest subset of updates that still passes instead of reverting
/// everything, and names the packages it held back.
/// </summary>
public sealed class PackageUpdateService(
    IProjectDiscoveryService discovery,
    IFileSystem fileSystem,
    INuGetFeedService feedService,
    IDotNetCliService cli,
    IBackupService backupService
) : IPackageUpdateService
{
    public async Task<PackageUpdateResult> UpdateAsync(
        PackageUpdateOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var propsPath = options.PackagesPropsPath
            ?? Path.Combine(options.RootDirectory, "Directory.Packages.props");

        if (!fileSystem.FileExists(propsPath))
        {
            return Empty("No Directory.Packages.props found. Run the migration first.");
        }

        var propsFile = await discovery.LoadAsync(propsPath, cancellationToken);
        if (propsFile is null)
        {
            return Empty($"Could not read {propsPath}.");
        }

        var original = propsFile.Content;
        var currentVersions = ReadVersions(original);

        if (currentVersions.Count == 0)
        {
            return Empty("Directory.Packages.props declares no PackageVersion entries.");
        }

        progress?.Report("Checking the feed for newer versions");

        var only = options.Only.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ids = currentVersions
            .Keys.Where(id => only.Count == 0 || only.Contains(id))
            .ToList();

        var metadata = await feedService.GetMetadataAsync(
            ids,
            options.MaxParallelism,
            null,
            cancellationToken
        );

        var candidates = BuildCandidates(currentVersions, metadata, options.IncludePrerelease);

        if (candidates.Count == 0)
        {
            return new PackageUpdateResult
            {
                Candidates = [],
                Applied = [],
                HeldBack = [],
                Message = "Everything is already up to date.",
            };
        }

        if (options.DryRun)
        {
            return new PackageUpdateResult
            {
                Candidates = candidates,
                Applied = [],
                HeldBack = [],
                Message = $"{candidates.Count} update(s) available (dry run).",
            };
        }

        var backup = options.CreateBackup
            ? await backupService.CreateAsync(
                options.RootDirectory,
                options.BackupDirectory ?? options.RootDirectory,
                "update",
                [propsPath],
                cancellationToken
            )
            : null;

        var target = options.SolutionPath ?? options.RootDirectory;

        if (options.SkipTests || !cli.IsAvailable)
        {
            await WriteAsync(propsFile, original, candidates, cancellationToken);

            return new PackageUpdateResult
            {
                Candidates = candidates,
                Applied = candidates,
                HeldBack = [],
                Backup = backup,
                Message = options.SkipTests
                    ? "Applied without running tests."
                    : "dotnet CLI unavailable; applied without running tests.",
            };
        }

        var cycles = 0;

        progress?.Report($"Applying {candidates.Count} update(s) and running tests");

        if (await TryAsync(propsFile, original, candidates, options, target, cancellationToken))
        {
            cycles++;
            return new PackageUpdateResult
            {
                Candidates = candidates,
                Applied = candidates,
                HeldBack = [],
                TestsRun = true,
                TestsPassed = true,
                BisectCycles = cycles,
                Backup = backup,
                Message = $"All {candidates.Count} update(s) applied; tests passed.",
            };
        }

        cycles++;

        if (!options.Bisect)
        {
            await RestoreAsync(propsFile, original, cancellationToken);

            return new PackageUpdateResult
            {
                Candidates = candidates,
                Applied = [],
                HeldBack = candidates,
                TestsRun = true,
                TestsPassed = false,
                BisectCycles = cycles,
                Backup = backup,
                RolledBack = true,
                Message = "Tests failed; all updates were rolled back. Re-run with --bisect to keep the green subset.",
            };
        }

        progress?.Report("Tests failed; bisecting for the largest green subset");

        var budget = Math.Max(2, options.BisectBudget);
        var green = await BisectAsync(
            propsFile,
            original,
            candidates,
            options,
            target,
            budget,
            cycles,
            progress,
            cancellationToken
        );

        cycles = green.Cycles;

        if (green.Subset.Count == 0)
        {
            await RestoreAsync(propsFile, original, cancellationToken);

            return new PackageUpdateResult
            {
                Candidates = candidates,
                Applied = [],
                HeldBack = candidates,
                TestsRun = true,
                TestsPassed = false,
                BisectCycles = cycles,
                Backup = backup,
                RolledBack = true,
                Message = "No subset of the updates passed; everything was rolled back.",
            };
        }

        await WriteAsync(propsFile, original, green.Subset, cancellationToken);

        var heldBack = candidates.Except(green.Subset).ToList();

        return new PackageUpdateResult
        {
            Candidates = candidates,
            Applied = green.Subset,
            HeldBack = heldBack,
            TestsRun = true,
            TestsPassed = true,
            BisectCycles = cycles,
            Backup = backup,
            Message =
                $"Kept {green.Subset.Count} of {candidates.Count} update(s). Held back: "
                + string.Join(", ", heldBack.Select(c => c.PackageId)),
        };
    }

    /// <summary>
    /// Divide and conquer: if a set fails, split it, keep whichever halves pass, then confirm the
    /// union. Each restore+test cycle is charged against the budget.
    /// </summary>
    private async Task<(List<PackageUpdateCandidate> Subset, int Cycles)> BisectAsync(
        ProjectFile propsFile,
        string original,
        IReadOnlyList<PackageUpdateCandidate> candidates,
        PackageUpdateOptions options,
        string target,
        int budget,
        int cycles,
        IProgress<string>? progress,
        CancellationToken cancellationToken
    )
    {
        if (candidates.Count == 0 || cycles >= budget)
        {
            return ([], cycles);
        }

        if (candidates.Count == 1)
        {
            progress?.Report($"Testing {candidates[0].PackageId} on its own");
            cycles++;

            var ok = await TryAsync(
                propsFile,
                original,
                candidates,
                options,
                target,
                cancellationToken
            );

            return (ok ? candidates.ToList() : [], cycles);
        }

        var middle = candidates.Count / 2;
        var left = candidates.Take(middle).ToList();
        var right = candidates.Skip(middle).ToList();

        var leftResult = await TestSubsetAsync(left);
        var rightResult = await TestSubsetAsync(right);

        List<PackageUpdateCandidate> best;

        if (leftResult.Green && rightResult.Green)
        {
            // Both halves pass alone; confirm they also pass together.
            if (cycles < budget)
            {
                cycles++;
                var combined = left.Concat(right).ToList();
                if (await TryAsync(propsFile, original, combined, options, target, cancellationToken))
                {
                    return (combined, cycles);
                }
            }

            best = left.Count >= right.Count ? leftResult.Subset : rightResult.Subset;
        }
        else
        {
            best = leftResult.Subset.Count >= rightResult.Subset.Count
                ? leftResult.Subset
                : rightResult.Subset;
        }

        return (best, cycles);

        async Task<(bool Green, List<PackageUpdateCandidate> Subset)> TestSubsetAsync(
            List<PackageUpdateCandidate> subset
        )
        {
            if (subset.Count == 0 || cycles >= budget)
            {
                return (false, []);
            }

            progress?.Report($"Testing a subset of {subset.Count} update(s)");
            cycles++;

            if (await TryAsync(propsFile, original, subset, options, target, cancellationToken))
            {
                return (true, subset);
            }

            var (nested, nestedCycles) = await BisectAsync(
                propsFile,
                original,
                subset,
                options,
                target,
                budget,
                cycles,
                progress,
                cancellationToken
            );

            cycles = nestedCycles;
            return (false, nested);
        }
    }

    private async Task<bool> TryAsync(
        ProjectFile propsFile,
        string original,
        IReadOnlyList<PackageUpdateCandidate> subset,
        PackageUpdateOptions options,
        string target,
        CancellationToken cancellationToken
    )
    {
        await WriteAsync(propsFile, original, subset, cancellationToken);

        var restore = await cli.RestoreAsync(target, options.RootDirectory, cancellationToken);
        if (!restore.Succeeded)
        {
            return false;
        }

        var test = await cli.TestAsync(
            target,
            options.RootDirectory,
            options.TestFilter,
            cancellationToken
        );

        return test.Succeeded;
    }

    private async Task WriteAsync(
        ProjectFile propsFile,
        string original,
        IReadOnlyList<PackageUpdateCandidate> subset,
        CancellationToken cancellationToken
    )
    {
        var content = original;

        foreach (var candidate in subset)
        {
            content =
                PackagesPropsEditor.SetVersion(content, candidate.PackageId, candidate.NewVersion)
                ?? content;
        }

        await fileSystem.WriteAllBytesAsync(
            propsFile.Path,
            EncodingDetector.Encode(content, propsFile.Format),
            cancellationToken
        );
    }

    private Task RestoreAsync(
        ProjectFile propsFile,
        string original,
        CancellationToken cancellationToken
    ) =>
        fileSystem.WriteAllBytesAsync(
            propsFile.Path,
            EncodingDetector.Encode(original, propsFile.Format),
            cancellationToken
        );

    private static List<PackageUpdateCandidate> BuildCandidates(
        IReadOnlyDictionary<string, string> current,
        IReadOnlyDictionary<string, PackageMetadataSnapshot> metadata,
        bool includePrerelease
    )
    {
        var candidates = new List<PackageUpdateCandidate>();

        foreach (var (packageId, version) in current.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (
                !metadata.TryGetValue(packageId, out var snapshot)
                || !snapshot.Found
                || !NuGetVersion.TryParse(version, out var currentVersion)
            )
            {
                continue;
            }

            var latest = includePrerelease
                ? snapshot.LatestIncludingPrerelease
                : snapshot.LatestStable;

            if (latest is null || latest <= currentVersion)
            {
                continue;
            }

            candidates.Add(
                new PackageUpdateCandidate
                {
                    PackageId = packageId,
                    CurrentVersion = version,
                    NewVersion = latest.ToNormalizedString(),
                    IsMajorUpgrade = latest.Major > currentVersion.Major,
                }
            );
        }

        return candidates;
    }

    private static IReadOnlyDictionary<string, string> ReadVersions(string content) =>
        Xml.MsBuildXml.TryParse(content, out var document, out _)
            ? Xml.PackageReferenceReader.ReadPackageVersions(document)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static PackageUpdateResult Empty(string message) =>
        new()
        {
            Candidates = [],
            Applied = [],
            HeldBack = [],
            Message = message,
        };
}
