using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Configuration;
using CentralConfigGenerator.Core.Tests.TestSupport;

namespace CentralConfigGenerator.Core.Tests.Analysis;

public class ConfigurationLoaderTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ccg-config");

    [Fact]
    public async Task LoadAsync_ShouldReadEveryKnownSetting()
    {
        var json = """
            {
              "conflictStrategy": "Lowest",
              "backup": false,
              "failOn": "Critical",
              "transitivePinning": true,
              "excludeDirs": "samples",
              "rules": { "OutdatedPackage": "none" },
              "retention": { "enabled": true, "maxBackups": 3 },
              "ignorePackages": [ "Internal.*" ]
            }
            """;

        var fileSystem = new InMemoryFileSystem().AddFile(
            Path.Combine(Root, ConfigurationLoader.FileName),
            json
        );

        var (settings, path, error) = await new ConfigurationLoader(fileSystem).LoadAsync(Root);

        error.ShouldBeNull();
        path.ShouldNotBeNull();
        settings.ShouldNotBeNull();
        settings.ConflictStrategy.ShouldBe(VersionResolutionStrategyName.Lowest);
        settings.Backup.ShouldBe(false);
        settings.FailOn.ShouldBe(Severity.Critical);
        settings.TransitivePinning.ShouldBe(true);
        settings.Retention!.MaxBackups.ShouldBe(3);
        settings.IgnorePackages.ShouldNotBeNull().ShouldContain("Internal.*");
    }

    [Fact]
    public async Task LoadAsync_ShouldWalkUpTheDirectoryTree()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, ConfigurationLoader.FileName), "{}")
            .AddDirectory(Path.Combine(Root, "src", "App"));

        var (settings, _, _) = await new ConfigurationLoader(fileSystem).LoadAsync(
            Path.Combine(Root, "src", "App")
        );

        settings.ShouldNotBeNull();
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnNothingWhenThereIsNoFile()
    {
        var (settings, path, error) = await new ConfigurationLoader(
            new InMemoryFileSystem()
        ).LoadAsync(Root);

        settings.ShouldBeNull();
        path.ShouldBeNull();
        error.ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_ShouldReportMalformedJson()
    {
        var fileSystem = new InMemoryFileSystem().AddFile(
            Path.Combine(Root, ConfigurationLoader.FileName),
            "{ not json"
        );

        var (settings, _, error) = await new ConfigurationLoader(fileSystem).LoadAsync(Root);

        settings.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public async Task LoadAsync_ShouldTolerateCommentsAndTrailingCommas()
    {
        var json = """
            {
              // the default strategy
              "conflictStrategy": "Highest",
            }
            """;

        var fileSystem = new InMemoryFileSystem().AddFile(
            Path.Combine(Root, ConfigurationLoader.FileName),
            json
        );

        var (settings, _, error) = await new ConfigurationLoader(fileSystem).LoadAsync(Root);

        error.ShouldBeNull();
        settings!.ConflictStrategy.ShouldBe(VersionResolutionStrategyName.Highest);
    }

    [Fact]
    public async Task ScaffoldAsync_ShouldWriteAConfigThatLoadsBack()
    {
        var fileSystem = new InMemoryFileSystem();
        var loader = new ConfigurationLoader(fileSystem);

        var path = await loader.ScaffoldAsync(Root, overwrite: false);

        fileSystem.ReadText(path).ShouldContain("$schema");

        var (settings, _, error) = await loader.LoadAsync(Root);
        error.ShouldBeNull();
        settings.ShouldNotBeNull();
    }

    [Fact]
    public async Task ScaffoldAsync_ShouldNotOverwriteWithoutPermission()
    {
        var path = Path.Combine(Root, ConfigurationLoader.FileName);
        var fileSystem = new InMemoryFileSystem().AddFile(path, "{ \"backup\": false }");

        await new ConfigurationLoader(fileSystem).ScaffoldAsync(Root, overwrite: false);

        fileSystem.ReadText(path).ShouldBe("{ \"backup\": false }");
    }
}
