using CentralConfigGenerator.Core.Services;
using CentralConfigGenerator.Core.Tests.TestSupport;

namespace CentralConfigGenerator.Core.Tests.Services;

public class BackupServiceTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ccg-backup");

    [Fact]
    public async Task CreateAsync_ShouldCopyEveryRequestedFile()
    {
        var path = Path.Combine(Root, "App.csproj");
        var fileSystem = new InMemoryFileSystem().AddFile(path, "<Project />");
        var service = new BackupService(fileSystem);

        var backup = await service.CreateAsync(Root, Root, "migrate", [path]);

        backup.ShouldNotBeNull();
        backup.Files.Count.ShouldBe(1);
        backup.Operation.ShouldBe("migrate");
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnNullWhenNoFilesAreGiven()
    {
        var service = new BackupService(new InMemoryFileSystem());

        (await service.CreateAsync(Root, Root, "migrate", [])).ShouldBeNull();
    }

    [Fact]
    public async Task RestoreAsync_ShouldPutTheOriginalContentBack()
    {
        var path = Path.Combine(Root, "App.csproj");
        var fileSystem = new InMemoryFileSystem().AddFile(path, "original");
        var service = new BackupService(fileSystem);

        var backup = await service.CreateAsync(Root, Root, "migrate", [path]);
        await fileSystem.WriteAllTextAsync(path, "modified");

        await service.RestoreAsync(Root, backup!.Id);

        fileSystem.ReadText(path).ShouldBe("original");
    }

    [Fact]
    public async Task RestoreAsync_ShouldDeleteFilesThatTheOperationCreated()
    {
        var path = Path.Combine(Root, "Directory.Packages.props");
        var fileSystem = new InMemoryFileSystem().AddDirectory(Root);
        var service = new BackupService(fileSystem);

        // The file does not exist yet, so the backup records it as "created".
        var backup = await service.CreateAsync(Root, Root, "migrate", [path]);
        await fileSystem.WriteAllTextAsync(path, "<Project />");

        await service.RestoreAsync(Root, backup!.Id);

        fileSystem.FileExists(path).ShouldBeFalse();
    }

    [Fact]
    public async Task RestoreLatestAsync_ShouldPickTheNewestBackup()
    {
        var path = Path.Combine(Root, "App.csproj");
        var fileSystem = new InMemoryFileSystem().AddFile(path, "v1");
        var service = new BackupService(fileSystem);

        await service.CreateAsync(Root, Root, "first", [path]);
        await fileSystem.WriteAllTextAsync(path, "v2");
        await Task.Delay(5);
        await service.CreateAsync(Root, Root, "second", [path]);
        await fileSystem.WriteAllTextAsync(path, "v3");

        var restored = await service.RestoreLatestAsync(Root);

        restored!.Operation.ShouldBe("second");
        fileSystem.ReadText(path).ShouldBe("v2");
    }

    [Fact]
    public async Task List_ShouldReturnBackupsNewestFirst()
    {
        var path = Path.Combine(Root, "App.csproj");
        var fileSystem = new InMemoryFileSystem().AddFile(path, "x");
        var service = new BackupService(fileSystem);

        await service.CreateAsync(Root, Root, "first", [path]);
        await Task.Delay(5);
        await service.CreateAsync(Root, Root, "second", [path]);

        var backups = service.List(Root);

        backups.Count.ShouldBe(2);
        backups[0].Operation.ShouldBe("second");
    }

    [Fact]
    public async Task Prune_ShouldKeepOnlyTheRetainedBackups()
    {
        var path = Path.Combine(Root, "App.csproj");
        var fileSystem = new InMemoryFileSystem().AddFile(path, "x");
        var service = new BackupService(fileSystem);

        for (var i = 0; i < 4; i++)
        {
            await service.CreateAsync(Root, Root, $"op{i}", [path]);
            await Task.Delay(5);
        }

        var removed = service.Prune(Root, retention: 2);

        removed.Count.ShouldBe(2);
        service.List(Root).Count.ShouldBe(2);
    }

    [Fact]
    public async Task PruneAll_ShouldRemoveEverything()
    {
        var path = Path.Combine(Root, "App.csproj");
        var fileSystem = new InMemoryFileSystem().AddFile(path, "x");
        var service = new BackupService(fileSystem);

        await service.CreateAsync(Root, Root, "op", [path]);

        service.PruneAll(Root);

        service.List(Root).ShouldBeEmpty();
    }

    [Fact]
    public async Task EnsureGitIgnoreAsync_ShouldAppendTheBackupFolder()
    {
        var fileSystem = new InMemoryFileSystem().AddFile(Path.Combine(Root, ".gitignore"), "bin/\n");
        var service = new BackupService(fileSystem);

        await service.EnsureGitIgnoreAsync(Root, BackupService.DefaultBackupFolderName);

        var content = fileSystem.ReadText(Path.Combine(Root, ".gitignore"));
        content.ShouldContain("bin/");
        content.ShouldContain(".centralconfig-backups/");
    }

    [Fact]
    public async Task EnsureGitIgnoreAsync_ShouldNotDuplicateAnExistingEntry()
    {
        var path = Path.Combine(Root, ".gitignore");
        var fileSystem = new InMemoryFileSystem().AddFile(path, ".centralconfig-backups/\n");
        var service = new BackupService(fileSystem);

        await service.EnsureGitIgnoreAsync(Root, BackupService.DefaultBackupFolderName);

        fileSystem.ReadText(path).Split(".centralconfig-backups/").Length.ShouldBe(2);
    }
}
