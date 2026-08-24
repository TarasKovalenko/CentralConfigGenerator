using System.Text.Json;
using CentralConfigGenerator.Core.IO;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Services.Abstractions;

namespace CentralConfigGenerator.Core.Services;

/// <summary>
/// Creates timestamped backups of every file an operation is about to touch, and restores them
/// on demand. Backups live under <c>.centralconfig-backups/&lt;timestamp&gt;</c>.
/// </summary>
public sealed class BackupService(IFileSystem fileSystem) : IBackupService
{
    public const string DefaultBackupFolderName = ".centralconfig-backups";
    private const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<BackupSet?> CreateAsync(
        string rootDirectory,
        string backupDirectory,
        string operation,
        IEnumerable<string> files,
        CancellationToken cancellationToken = default
    )
    {
        var paths = files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (paths.Count == 0)
        {
            return null;
        }

        var id = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var root = ResolveRoot(backupDirectory);
        var target = Path.Combine(root, id);
        fileSystem.CreateDirectory(target);

        var manifest = new BackupManifest
        {
            Id = id,
            CreatedAt = DateTimeOffset.UtcNow,
            Operation = operation,
            RootDirectory = Path.GetFullPath(rootDirectory),
        };

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var full = Path.GetFullPath(path);
            var relative = MakeRelative(manifest.RootDirectory, full);
            var destination = Path.Combine(target, relative);

            if (fileSystem.FileExists(full))
            {
                fileSystem.CopyFile(full, destination, overwrite: true);
                manifest.Entries.Add(
                    new BackupEntry { OriginalPath = full, RelativePath = relative }
                );
            }
            else
            {
                manifest.Entries.Add(
                    new BackupEntry
                    {
                        OriginalPath = full,
                        RelativePath = relative,
                        WasCreated = true,
                    }
                );
            }
        }

        await fileSystem.WriteAllTextAsync(
            Path.Combine(target, ManifestFileName),
            JsonSerializer.Serialize(manifest, JsonOptions),
            cancellationToken
        );

        return ToBackupSet(manifest, target);
    }

    public IReadOnlyList<BackupSet> List(string backupDirectory)
    {
        var root = ResolveRoot(backupDirectory);
        if (!fileSystem.DirectoryExists(root))
        {
            return [];
        }

        var results = new List<BackupSet>();

        foreach (var directory in fileSystem.EnumerateDirectories(root))
        {
            var manifest = ReadManifest(directory);
            if (manifest is not null)
            {
                results.Add(ToBackupSet(manifest, directory));
            }
        }

        return results.OrderByDescending(b => b.CreatedAt).ToList();
    }

    public Task<BackupSet?> RestoreLatestAsync(
        string backupDirectory,
        CancellationToken cancellationToken = default
    )
    {
        var latest = List(backupDirectory).FirstOrDefault();
        return latest is null
            ? Task.FromResult<BackupSet?>(null)
            : RestoreAsync(backupDirectory, latest.Id, cancellationToken);
    }

    public async Task<BackupSet?> RestoreAsync(
        string backupDirectory,
        string backupId,
        CancellationToken cancellationToken = default
    )
    {
        var root = ResolveRoot(backupDirectory);
        var target = Path.Combine(root, backupId);
        var manifest = ReadManifest(target);

        if (manifest is null)
        {
            return null;
        }

        foreach (var entry in manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.WasCreated)
            {
                if (fileSystem.FileExists(entry.OriginalPath))
                {
                    fileSystem.DeleteFile(entry.OriginalPath);
                }

                continue;
            }

            var source = Path.Combine(target, entry.RelativePath);
            if (fileSystem.FileExists(source))
            {
                fileSystem.CopyFile(source, entry.OriginalPath, overwrite: true);
            }
        }

        await Task.CompletedTask;
        return ToBackupSet(manifest, target);
    }

    public IReadOnlyList<BackupSet> Prune(string backupDirectory, int retention)
    {
        if (retention < 0)
        {
            retention = 0;
        }

        var all = List(backupDirectory);
        var removed = all.Skip(retention).ToList();

        foreach (var backup in removed)
        {
            fileSystem.DeleteDirectory(backup.Path, recursive: true);
        }

        return removed;
    }

    public IReadOnlyList<BackupSet> PruneAll(string backupDirectory) => Prune(backupDirectory, 0);

    public async Task EnsureGitIgnoreAsync(
        string gitIgnoreDirectory,
        string backupDirectoryName,
        CancellationToken cancellationToken = default
    )
    {
        var path = Path.Combine(gitIgnoreDirectory, ".gitignore");
        var entry = backupDirectoryName.TrimEnd('/') + "/";

        var existing = fileSystem.FileExists(path)
            ? await fileSystem.ReadAllTextAsync(path, cancellationToken)
            : string.Empty;

        var lines = existing.Replace("\r\n", "\n").Split('\n');
        if (lines.Any(l => string.Equals(l.Trim(), entry, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var separator = existing.Length == 0 || existing.EndsWith('\n') ? string.Empty : "\n";
        var content =
            $"{existing}{separator}\n# CentralConfigGenerator backups\n{entry}\n";

        await fileSystem.WriteAllTextAsync(path, content, cancellationToken);
    }

    private static string ResolveRoot(string backupDirectory)
    {
        var full = Path.GetFullPath(backupDirectory);
        return string.Equals(
            Path.GetFileName(full),
            DefaultBackupFolderName,
            StringComparison.OrdinalIgnoreCase
        )
            ? full
            : Path.Combine(full, DefaultBackupFolderName);
    }

    private static string MakeRelative(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);

        // Files outside the scanned root are flattened so they still land inside the backup.
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            var sanitized = path.Replace(Path.VolumeSeparatorChar, '_')
                .Replace(Path.DirectorySeparatorChar, '_')
                .Replace(Path.AltDirectorySeparatorChar, '_');
            return Path.Combine("_external", sanitized);
        }

        return relative;
    }

    private BackupManifest? ReadManifest(string directory)
    {
        var manifestPath = Path.Combine(directory, ManifestFileName);
        if (!fileSystem.FileExists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = fileSystem.ReadAllTextAsync(manifestPath).GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<BackupManifest>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    private BackupSet ToBackupSet(BackupManifest manifest, string path)
    {
        long size = 0;
        foreach (var entry in manifest.Entries.Where(e => !e.WasCreated))
        {
            var file = Path.Combine(path, entry.RelativePath);
            if (fileSystem.FileExists(file))
            {
                size += fileSystem.GetFileSize(file);
            }
        }

        return new BackupSet
        {
            Id = manifest.Id,
            Path = path,
            CreatedAt = manifest.CreatedAt,
            Operation = manifest.Operation,
            Files = manifest.Entries.Select(e => e.OriginalPath).ToList(),
            SizeInBytes = size,
        };
    }
}
