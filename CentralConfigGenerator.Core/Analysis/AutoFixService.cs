using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Core.IO;
using CentralConfigGenerator.Core.Migration;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Services.Abstractions;

namespace CentralConfigGenerator.Core.Analysis;

/// <summary>The outcome of trying to auto-fix a set of findings.</summary>
public sealed record AutoFixResult
{
    public required IReadOnlyList<FileChange> Changes { get; init; }

    public required IReadOnlyList<Finding> Fixed { get; init; }

    public required IReadOnlyList<Finding> Skipped { get; init; }

    public BackupSet? Backup { get; init; }

    public bool HasChanges => Changes.Count > 0;
}

public interface IAutoFixService
{
    Task<AutoFixResult> PlanAsync(
        AnalysisContext context,
        IReadOnlyList<Finding> findings,
        CancellationToken cancellationToken = default
    );

    Task<BackupSet?> ApplyAsync(
        AutoFixResult result,
        string rootDirectory,
        string? backupDirectory,
        bool createBackup,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Turns auto-fixable findings into concrete file edits. Everything is planned in memory first so
/// <c>--fix-dry-run</c> and <c>--fix</c> share the same code path.
/// </summary>
public sealed class AutoFixService(
    IProjectDiscoveryService discovery,
    IFileSystem fileSystem,
    IBackupService backupService
) : IAutoFixService
{
    public async Task<AutoFixResult> PlanAsync(
        AnalysisContext context,
        IReadOnlyList<Finding> findings,
        CancellationToken cancellationToken = default
    )
    {
        // Working copies keyed by path, so several fixes can stack on the same file.
        var buffers = new Dictionary<string, (string Original, string Current, TextFileFormat Format)>(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var project in context.Projects)
        {
            buffers[project.Path] = (project.Content, project.Content, project.Format);
        }

        var propsPath = context.PackagesPropsPath;
        if (propsPath is not null && !buffers.ContainsKey(propsPath))
        {
            var props = await discovery.LoadAsync(propsPath, cancellationToken);
            if (props is not null)
            {
                buffers[propsPath] = (props.Content, props.Content, props.Format);
            }
        }

        var fixedFindings = new List<Finding>();
        var skipped = new List<Finding>();

        foreach (var finding in findings.OrderByDescending(f => f.Severity))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!finding.IsAutoFixable)
            {
                skipped.Add(finding);
                continue;
            }

            if (TryApply(finding, buffers, propsPath))
            {
                fixedFindings.Add(finding);
            }
            else
            {
                skipped.Add(finding);
            }
        }

        var changes = buffers
            .Where(b => !string.Equals(b.Value.Original, b.Value.Current, StringComparison.Ordinal))
            .Select(b => new FileChange
            {
                Path = b.Key,
                Kind = FileChangeKind.Modified,
                OriginalContent = b.Value.Original,
                NewContent = b.Value.Current,
                Format = b.Value.Format,
            })
            .OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AutoFixResult
        {
            Changes = changes,
            Fixed = fixedFindings,
            Skipped = skipped,
        };
    }

    public async Task<BackupSet?> ApplyAsync(
        AutoFixResult result,
        string rootDirectory,
        string? backupDirectory,
        bool createBackup,
        CancellationToken cancellationToken = default
    )
    {
        if (!result.HasChanges)
        {
            return null;
        }

        BackupSet? backup = null;

        if (createBackup)
        {
            backup = await backupService.CreateAsync(
                rootDirectory,
                backupDirectory ?? rootDirectory,
                "analyze --fix",
                result.Changes.Select(c => c.Path),
                cancellationToken
            );
        }

        foreach (var change in result.Changes)
        {
            var bytes = EncodingDetector.Encode(change.NewContent, change.Format);
            await fileSystem.WriteAllBytesAsync(change.Path, bytes, cancellationToken);
        }

        return backup;
    }

    private static bool TryApply(
        Finding finding,
        Dictionary<string, (string Original, string Current, TextFileFormat Format)> buffers,
        string? propsPath
    )
    {
        switch (finding.Fix)
        {
            case FixKind.RemoveInlineVersion:
                return finding.ProjectPath is not null
                    && finding.PackageId is not null
                    && Edit(
                        buffers,
                        finding.ProjectPath,
                        content =>
                            ProjectRewriter.StripVersion(
                                Wrap(finding.ProjectPath, content),
                                finding.PackageId
                            )
                    );

            case FixKind.SetCentralVersion:
                return propsPath is not null
                    && finding.PackageId is not null
                    && finding.FixValue is not null
                    && Edit(
                        buffers,
                        propsPath,
                        content =>
                            PackagesPropsEditor.SetVersion(
                                content,
                                finding.PackageId,
                                finding.FixValue
                            )
                    );

            case FixKind.RemoveOrphanedPackageVersion:
                return propsPath is not null
                    && finding.PackageId is not null
                    && Edit(
                        buffers,
                        propsPath,
                        content => PackagesPropsEditor.RemoveEntry(content, finding.PackageId)
                    );

            case FixKind.RemoveDuplicateReference:
                return finding.ProjectPath is not null
                    && finding.PackageId is not null
                    && Edit(
                        buffers,
                        finding.ProjectPath,
                        content =>
                            ProjectRewriter.RemoveDuplicateReference(
                                Wrap(finding.ProjectPath, content),
                                finding.PackageId
                            )
                    );

            case FixKind.NormalizePackageCasing:
            {
                if (finding.FixValue is null)
                {
                    return false;
                }

                var canonical = finding.FixValue;
                var changed = false;

                foreach (var path in buffers.Keys.ToList())
                {
                    changed |= Edit(
                        buffers,
                        path,
                        content =>
                            string.Equals(path, propsPath, StringComparison.OrdinalIgnoreCase)
                                ? PackagesPropsEditor.NormalizeCasing(content, canonical)
                                : ProjectRewriter.NormalizeCasing(Wrap(path, content), canonical)
                    );
                }

                return changed;
            }

            default:
                return false;
        }
    }

    private static ProjectFile Wrap(string path, string content) =>
        new() { Path = path, Content = content };

    private static bool Edit(
        Dictionary<string, (string Original, string Current, TextFileFormat Format)> buffers,
        string path,
        Func<string, string?> transform
    )
    {
        if (!buffers.TryGetValue(path, out var buffer))
        {
            return false;
        }

        var updated = transform(buffer.Current);
        if (updated is null)
        {
            return false;
        }

        buffers[path] = (buffer.Original, updated, buffer.Format);
        return true;
    }
}
