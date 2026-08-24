using System.Collections.Concurrent;
using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.IO;
using CentralConfigGenerator.Core.Migration;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Workspace;

public sealed record BatchOptions
{
    public required string RootDirectory { get; init; }

    /// <summary>Process repositories concurrently.</summary>
    public bool Parallel { get; init; }

    /// <summary>Keep going after a repository fails instead of stopping at the first error.</summary>
    public bool ContinueOnError { get; init; }

    public int MaxParallelism { get; init; } = 4;

    public string? ExcludePattern { get; init; }

    /// <summary>Maximum directory depth to search for solutions.</summary>
    public int MaxDepth { get; init; } = 4;
}

public sealed record BatchItemResult
{
    public required string Workspace { get; init; }

    public required bool Succeeded { get; init; }

    public MigrationPlan? Plan { get; init; }

    public string? Error { get; init; }
}

public sealed record BatchResult
{
    public required IReadOnlyList<BatchItemResult> Items { get; init; }

    public int SucceededCount => Items.Count(i => i.Succeeded);

    public int FailedCount => Items.Count(i => !i.Succeeded);
}

/// <summary>
/// Runs an operation across every solution in a monorepo. Each workspace is independent, so
/// failures are isolated and (optionally) skipped rather than aborting the batch.
/// </summary>
public sealed class BatchService(IFileSystem fileSystem)
{
    /// <summary>Finds each distinct solution directory beneath the root.</summary>
    public IReadOnlyList<string> DiscoverWorkspaces(BatchOptions options)
    {
        var root = Path.GetFullPath(options.RootDirectory);

        if (!fileSystem.DirectoryExists(root))
        {
            return [];
        }

        var excludeRegex = string.IsNullOrWhiteSpace(options.ExcludePattern)
            ? null
            : new System.Text.RegularExpressions.Regex(
                options.ExcludePattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

        var solutions = fileSystem
            .EnumerateFiles(root, "*", recursive: true)
            .Where(SolutionReader.IsSolution)
            .Where(path => Depth(root, path) <= options.MaxDepth)
            .Where(path => !IsInExcludedDirectory(root, path, excludeRegex))
            .Select(path => Path.GetDirectoryName(path)!)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (solutions.Count > 0)
        {
            return solutions;
        }

        // No solutions: fall back to directories that directly contain project files.
        return fileSystem
            .EnumerateFiles(root, "*", recursive: true)
            .Where(p => ProjectKindExtensions.FromPath(p).IsProject())
            .Where(path => !IsInExcludedDirectory(root, path, excludeRegex))
            .Select(path => Path.GetDirectoryName(path)!)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<BatchResult> RunAsync(
        BatchOptions options,
        Func<string, CancellationToken, Task<MigrationPlan>> operation,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var workspaces = DiscoverWorkspaces(options);
        var results = new ConcurrentBag<BatchItemResult>();

        if (options.Parallel)
        {
            await Parallel.ForEachAsync(
                workspaces,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, options.MaxParallelism),
                    CancellationToken = cancellationToken,
                },
                async (workspace, token) =>
                {
                    results.Add(await RunOneAsync(workspace, operation, progress, token));
                }
            );
        }
        else
        {
            foreach (var workspace in workspaces)
            {
                var result = await RunOneAsync(workspace, operation, progress, cancellationToken);
                results.Add(result);

                if (!result.Succeeded && !options.ContinueOnError)
                {
                    break;
                }
            }
        }

        return new BatchResult
        {
            Items = results.OrderBy(r => r.Workspace, StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    private static async Task<BatchItemResult> RunOneAsync(
        string workspace,
        Func<string, CancellationToken, Task<MigrationPlan>> operation,
        IProgress<string>? progress,
        CancellationToken cancellationToken
    )
    {
        progress?.Report(workspace);

        try
        {
            var plan = await operation(workspace, cancellationToken);
            return new BatchItemResult
            {
                Workspace = workspace,
                Succeeded = true,
                Plan = plan,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new BatchItemResult
            {
                Workspace = workspace,
                Succeeded = false,
                Error = ex.Message,
            };
        }
    }

    private static int Depth(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
    }

    private static bool IsInExcludedDirectory(
        string root,
        string path,
        System.Text.RegularExpressions.Regex? excludeRegex
    ) =>
        ProjectDiscoveryService.IsExcluded(
            root,
            path,
            new DiscoveryOptions { RootDirectory = root },
            excludeRegex
        );
}
