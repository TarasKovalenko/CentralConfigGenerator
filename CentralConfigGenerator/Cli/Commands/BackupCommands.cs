using System.ComponentModel;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public class BackupSettings : CommandSettings
{
    [CommandOption("-d|--directory <PATH>")]
    [Description("Workspace directory holding the backups. Defaults to the current directory.")]
    public string? Directory { get; init; }

    [CommandOption("--backup-dir <PATH>")]
    [Description("Explicit backup directory, when it is not next to the workspace.")]
    public string? BackupDir { get; init; }

    [CommandOption("-q|--quiet")]
    public bool Quiet { get; init; }

    public string ResolvedBackupDirectory =>
        System.IO.Path.GetFullPath(
            BackupDir ?? Directory ?? System.IO.Directory.GetCurrentDirectory()
        );
}

/// <summary>Lists every backup this tool has taken.</summary>
public sealed class BackupListCommand(IBackupService backupService) : Command<BackupSettings>
{
    public override int Execute(CommandContext context, BackupSettings settings, CancellationToken cancellationToken)
    {
        Ui.Quiet = settings.Quiet;

        var backups = backupService.List(settings.ResolvedBackupDirectory);

        if (backups.Count == 0)
        {
            Ui.Info("[grey]No backups found.[/]");
            return ExitCode.Success;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Id");
        table.AddColumn("Created");
        table.AddColumn("Operation");
        table.AddColumn(new TableColumn("Files").RightAligned());
        table.AddColumn(new TableColumn("Size").RightAligned());

        foreach (var backup in backups)
        {
            table.AddRow(
                Markup.Escape(backup.Id),
                backup.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                Markup.Escape(backup.Operation),
                backup.Files.Count.ToString(),
                FormatSize(backup.SizeInBytes)
            );
        }

        AnsiConsole.Write(table);
        return ExitCode.Success;
    }

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB",
        };
}

public sealed class BackupRestoreSettings : BackupSettings
{
    [CommandArgument(0, "[BACKUP_ID]")]
    [Description("Backup to restore. Defaults to the most recent one.")]
    public string? BackupId { get; init; }

    [CommandOption("-f|--force")]
    [Description("Skip the confirmation prompt.")]
    public bool Force { get; init; }
}

/// <summary>Restores the files captured in a backup, undoing whatever changed them.</summary>
public sealed class BackupRestoreCommand(IBackupService backupService)
    : AsyncCommand<BackupRestoreSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        BackupRestoreSettings settings,
        CancellationToken cancellationToken
    )
    {
        Ui.Quiet = settings.Quiet;

        var directory = settings.ResolvedBackupDirectory;
        var backups = backupService.List(directory);

        if (backups.Count == 0)
        {
            Ui.Error("No backups found to restore.");
            return ExitCode.FileOperationError;
        }

        var target = settings.BackupId is null
            ? backups[0]
            : backups.FirstOrDefault(b =>
                string.Equals(b.Id, settings.BackupId, StringComparison.OrdinalIgnoreCase)
            );

        if (target is null)
        {
            Ui.Error($"Backup '{settings.BackupId}' was not found.");
            return ExitCode.ValidationError;
        }

        Ui.Info(
            $"Restoring backup [bold]{Markup.Escape(target.Id)}[/] "
                + $"({target.Files.Count} file(s), taken {target.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} by '{Markup.Escape(target.Operation)}')."
        );

        if (!settings.Force && !Ui.Confirm("This overwrites the current files. Continue?", defaultValue: false))
        {
            Ui.Warn("Cancelled.");
            return ExitCode.Success;
        }

        var restored = await backupService.RestoreAsync(directory, target.Id);

        if (restored is null)
        {
            Ui.Error("The backup manifest could not be read.");
            return ExitCode.FileOperationError;
        }

        Ui.Success($"Restored {restored.Files.Count} file(s) from backup {restored.Id}.");
        return ExitCode.Success;
    }
}

public sealed class BackupPruneSettings : BackupSettings
{
    [CommandOption("--retention <N>")]
    [Description("How many backups to keep. Default: 5.")]
    public int? Retention { get; init; }

    [CommandOption("--all")]
    [Description("Delete every backup.")]
    public bool All { get; init; }

    [CommandOption("-f|--force")]
    public bool Force { get; init; }
}

/// <summary>Deletes old backup sets.</summary>
public sealed class BackupPruneCommand(IBackupService backupService) : Command<BackupPruneSettings>
{
    public override int Execute(CommandContext context, BackupPruneSettings settings, CancellationToken cancellationToken)
    {
        Ui.Quiet = settings.Quiet;

        var directory = settings.ResolvedBackupDirectory;
        var retention = settings.All ? 0 : settings.Retention ?? 5;
        var existing = backupService.List(directory);
        var doomed = existing.Skip(retention).ToList();

        if (doomed.Count == 0)
        {
            Ui.Info($"[grey]Nothing to prune ({existing.Count} backup(s), keeping {retention}).[/]");
            return ExitCode.Success;
        }

        if (
            !settings.Force
            && !Ui.Confirm($"Delete {doomed.Count} backup set(s)? This cannot be undone.", defaultValue: false)
        )
        {
            Ui.Warn("Cancelled.");
            return ExitCode.Success;
        }

        var removed = settings.All
            ? backupService.PruneAll(directory)
            : backupService.Prune(directory, retention);

        Ui.Success($"Deleted {removed.Count} backup set(s).");
        return ExitCode.Success;
    }
}
