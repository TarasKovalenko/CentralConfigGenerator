using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Core.IO;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Services;
using CentralConfigGenerator.Core.Services.Abstractions;
using CentralConfigGenerator.Core.Xml;

namespace CentralConfigGenerator.Core.Migration;

/// <summary>
/// Plans and applies the migration between inline package versions and central package
/// management. Planning never writes to disk, which is what makes dry-run, diff and rollback
/// all fall out of the same code path.
/// </summary>
public sealed class MigrationService(
    IProjectDiscoveryService discovery,
    IFileSystem fileSystem,
    IBackupService backupService,
    VersionSelector versionSelector
) : IMigrationService
{
    public async Task<MigrationPlan> PlanAsync(
        MigrationOptions options,
        Func<PackageConflict, string>? conflictPrompt = null,
        CancellationToken cancellationToken = default
    )
    {
        var projects = await DiscoverAsync(options, cancellationToken);
        if (projects.Count == 0)
        {
            return EmptyPlan();
        }

        var references = projects
            .SelectMany(PackageReferenceReader.Read)
            .Where(r => !string.IsNullOrWhiteSpace(r.Version))
            .ToList();

        var grouped = references
            .GroupBy(r => r.PackageId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<PackageConflict>();
        var findings = new List<Finding>();

        foreach (var group in grouped)
        {
            var versions = group
                .Select(r => r.Version!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string chosen;

            if (versions.Count == 1)
            {
                chosen = versions[0];
            }
            else
            {
                var pending = new PackageConflict
                {
                    PackageId = group.Key,
                    References = group.ToList(),
                    ResolvedVersion = string.Empty,
                };

                if (options.FailOnConflict && conflictPrompt is null)
                {
                    throw new VersionConflictException(group.Key, versions);
                }

                chosen =
                    conflictPrompt?.Invoke(pending)
                    ?? versionSelector.Select(
                        group.Key,
                        versions,
                        options.ConflictStrategy,
                        options.IgnorePrerelease,
                        options.VersionComparison
                    );

                conflicts.Add(pending with { ResolvedVersion = chosen });

                findings.Add(
                    new Finding
                    {
                        RuleId = RuleIds.VersionInconsistency,
                        Severity = Severity.Moderate,
                        PackageId = group.Key,
                        Version = chosen,
                        Message =
                            $"'{group.Key}' is referenced with {versions.Count} different versions ({string.Join(", ", versions)}).",
                        Recommendation = $"Unified on {chosen}.",
                    }
                );
            }

            resolved[group.Key] = chosen;
        }

        var globals = references
            .Where(r => r.IsGlobal)
            .Select(r => r.PackageId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var changes = new List<FileChange>();

        changes.Add(
            await BuildPackagesPropsChangeAsync(options, resolved, globals, cancellationToken)
        );

        if (!options.KeepInlineVersions)
        {
            foreach (var project in projects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var updated = ProjectRewriter.StripVersions(project, out _);
                if (updated is not null)
                {
                    changes.Add(ToChange(project, updated));
                }
            }
        }

        if (options.UnifyProperties)
        {
            changes.AddRange(await BuildPropertyChangesAsync(options, projects, cancellationToken));
        }

        return new MigrationPlan
        {
            Changes = changes,
            ResolvedVersions = resolved,
            Conflicts = conflicts,
            Findings = findings,
            ProjectCount = projects.Count,
        };
    }

    public async Task<MigrationPlan> PlanRevertAsync(
        MigrationOptions options,
        CancellationToken cancellationToken = default
    )
    {
        var projects = await DiscoverAsync(options, cancellationToken);
        if (projects.Count == 0)
        {
            return EmptyPlan();
        }

        var propsPath = options.PackagesPropsPath;
        var centralVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var findings = new List<Finding>();

        if (fileSystem.FileExists(propsPath))
        {
            var propsFile = await discovery.LoadAsync(propsPath, cancellationToken);
            if (
                propsFile is not null
                && MsBuildXml.TryParse(propsFile.Content, out var propsDocument, out _)
            )
            {
                foreach (var pair in PackageReferenceReader.ReadPackageVersions(propsDocument))
                {
                    centralVersions[pair.Key] = pair.Value;
                }
            }
        }

        var changes = new List<FileChange>();

        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var updated = ProjectRewriter.RestoreVersions(project, centralVersions, out var missing);

            foreach (var packageId in missing)
            {
                findings.Add(
                    new Finding
                    {
                        RuleId = RuleIds.MissingCentralVersion,
                        Severity = Severity.High,
                        PackageId = packageId,
                        ProjectPath = project.Path,
                        Message =
                            $"'{packageId}' has no PackageVersion entry, so no version could be restored.",
                        Recommendation =
                            "Add the package to Directory.Packages.props or set the version manually.",
                    }
                );
            }

            if (updated is not null)
            {
                changes.Add(ToChange(project, updated));
            }
        }

        if (fileSystem.FileExists(propsPath))
        {
            var propsFile = await discovery.LoadAsync(propsPath, cancellationToken);
            if (propsFile is not null)
            {
                changes.Add(
                    new FileChange
                    {
                        Path = propsPath,
                        Kind = FileChangeKind.Deleted,
                        OriginalContent = propsFile.Content,
                        NewContent = string.Empty,
                        Format = propsFile.Format,
                    }
                );
            }
        }

        return new MigrationPlan
        {
            Changes = changes,
            ResolvedVersions = centralVersions,
            Conflicts = [],
            Findings = findings,
            ProjectCount = projects.Count,
        };
    }

    public async Task<BackupSet?> ApplyAsync(
        MigrationPlan plan,
        MigrationOptions options,
        CancellationToken cancellationToken = default
    )
    {
        BackupSet? backup = null;

        if (options.CreateBackup && plan.HasChanges)
        {
            backup = await backupService.CreateAsync(
                options.RootDirectory,
                options.BackupDirectory ?? options.RootDirectory,
                "migrate",
                plan.AffectedPaths,
                cancellationToken
            );
        }

        if (options.AddGitIgnore)
        {
            await backupService.EnsureGitIgnoreAsync(
                options.BackupDirectory ?? options.RootDirectory,
                BackupService.DefaultBackupFolderName,
                cancellationToken
            );
        }

        foreach (var change in plan.Changes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (change.Kind == FileChangeKind.Deleted)
            {
                if (fileSystem.FileExists(change.Path))
                {
                    fileSystem.DeleteFile(change.Path);
                }

                continue;
            }

            var format = ApplyFormatOverrides(change.Format, options);
            var bytes = EncodingDetector.Encode(change.NewContent, format);
            await fileSystem.WriteAllBytesAsync(change.Path, bytes, cancellationToken);
        }

        return backup;
    }

    private static TextFileFormat ApplyFormatOverrides(
        TextFileFormat format,
        MigrationOptions options
    )
    {
        var encoding = EncodingDetector.Resolve(options.EncodingName);

        return format with
        {
            Encoding = encoding ?? format.Encoding,
            HasByteOrderMark = encoding is not null
                ? encoding.GetPreamble().Length > 0 && encoding is not System.Text.UTF8Encoding
                : format.HasByteOrderMark,
            LineEnding =
                options.LineEnding == LineEndingStyle.Preserve
                    ? format.LineEnding
                    : options.LineEnding,
        };
    }

    private async Task<IReadOnlyList<ProjectFile>> DiscoverAsync(
        MigrationOptions options,
        CancellationToken cancellationToken
    ) =>
        await discovery.DiscoverAsync(
            new DiscoveryOptions
            {
                RootDirectory = options.RootDirectory,
                SolutionPath = options.SolutionPath,
                ProjectPath = options.ProjectPath,
                ExcludePattern = options.ExcludePattern,
                IncludedKinds = options.IncludedKinds.ToList(),
            },
            cancellationToken
        );

    private async Task<FileChange> BuildPackagesPropsChangeAsync(
        MigrationOptions options,
        IReadOnlyDictionary<string, string> resolved,
        IReadOnlyCollection<string> globals,
        CancellationToken cancellationToken
    )
    {
        var path = options.PackagesPropsPath;
        var writerOptions = new PackagesPropsOptions
        {
            TransitivePinning = options.TransitivePinning,
            GlobalPackages = globals,
        };

        if (fileSystem.FileExists(path))
        {
            var existing = await discovery.LoadAsync(path, cancellationToken);
            var original = existing?.Content ?? string.Empty;

            var content = options.Merge
                ? PackagesPropsWriter.Merge(original, resolved, writerOptions)
                : PackagesPropsWriter.Create(resolved, writerOptions);

            return new FileChange
            {
                Path = path,
                Kind = FileChangeKind.Modified,
                OriginalContent = original,
                NewContent = content,
                Format = existing?.Format ?? TextFileFormat.Default,
            };
        }

        return new FileChange
        {
            Path = path,
            Kind = FileChangeKind.Created,
            OriginalContent = string.Empty,
            NewContent = PackagesPropsWriter.Create(resolved, writerOptions),
            Format = TextFileFormat.Default,
        };
    }

    private async Task<IReadOnlyList<FileChange>> BuildPropertyChangesAsync(
        MigrationOptions options,
        IReadOnlyList<ProjectFile> projects,
        CancellationToken cancellationToken
    )
    {
        var hoisted = PropertyHoister.FindHoistableProperties(projects, options.BuildProperties);
        if (hoisted.Count == 0)
        {
            return [];
        }

        var changes = new List<FileChange>();
        var path = options.BuildPropsPath;

        if (fileSystem.FileExists(path))
        {
            var existing = await discovery.LoadAsync(path, cancellationToken);
            var original = existing?.Content ?? string.Empty;

            changes.Add(
                new FileChange
                {
                    Path = path,
                    Kind = FileChangeKind.Modified,
                    OriginalContent = original,
                    NewContent = BuildPropsWriter.Merge(original, hoisted, options.BuildProperties),
                    Format = existing?.Format ?? TextFileFormat.Default,
                }
            );
        }
        else
        {
            changes.Add(
                new FileChange
                {
                    Path = path,
                    Kind = FileChangeKind.Created,
                    OriginalContent = string.Empty,
                    NewContent = BuildPropsWriter.Create(hoisted, options.BuildProperties),
                    Format = TextFileFormat.Default,
                }
            );
        }

        foreach (var project in projects)
        {
            var updated = ProjectRewriter.RemoveProperties(project, hoisted.Keys, hoisted);
            if (updated is null)
            {
                continue;
            }

            var existingChange = changes.FirstOrDefault(c =>
                string.Equals(c.Path, project.Path, StringComparison.OrdinalIgnoreCase)
            );

            if (existingChange is not null)
            {
                changes.Remove(existingChange);
            }

            changes.Add(ToChange(project, updated));
        }

        return changes;
    }

    private static FileChange ToChange(ProjectFile project, string updatedContent) =>
        new()
        {
            Path = project.Path,
            Kind = FileChangeKind.Modified,
            OriginalContent = project.Content,
            NewContent = updatedContent,
            Format = project.Format,
        };

    private static MigrationPlan EmptyPlan() =>
        new()
        {
            Changes = [],
            ResolvedVersions = new Dictionary<string, string>(),
            Conflicts = [],
            Findings = [],
            ProjectCount = 0,
        };
}
