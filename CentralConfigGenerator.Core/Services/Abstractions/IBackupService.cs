using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Services.Abstractions;

public interface IBackupService
{
    Task<BackupSet?> CreateAsync(
        string rootDirectory,
        string backupDirectory,
        string operation,
        IEnumerable<string> files,
        CancellationToken cancellationToken = default
    );

    IReadOnlyList<BackupSet> List(string backupDirectory);

    Task<BackupSet?> RestoreLatestAsync(
        string backupDirectory,
        CancellationToken cancellationToken = default
    );

    Task<BackupSet?> RestoreAsync(
        string backupDirectory,
        string backupId,
        CancellationToken cancellationToken = default
    );

    IReadOnlyList<BackupSet> Prune(string backupDirectory, int retention);

    IReadOnlyList<BackupSet> PruneAll(string backupDirectory);

    Task EnsureGitIgnoreAsync(
        string gitIgnoreDirectory,
        string backupDirectoryName,
        CancellationToken cancellationToken = default
    );
}
