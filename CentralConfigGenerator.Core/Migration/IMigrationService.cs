using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Migration;

public interface IMigrationService
{
    /// <summary>Plans a migration to central package management without touching disk.</summary>
    Task<MigrationPlan> PlanAsync(
        MigrationOptions options,
        Func<PackageConflict, string>? conflictPrompt = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Plans the reverse migration: central versions pushed back into project files.</summary>
    Task<MigrationPlan> PlanRevertAsync(
        MigrationOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>Writes a plan to disk, taking a backup first unless disabled.</summary>
    Task<BackupSet?> ApplyAsync(
        MigrationPlan plan,
        MigrationOptions options,
        CancellationToken cancellationToken = default
    );
}
