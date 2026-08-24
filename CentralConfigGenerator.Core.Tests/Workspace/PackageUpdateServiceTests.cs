using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.Services;
using CentralConfigGenerator.Core.Tests.TestSupport;
using CentralConfigGenerator.Core.Workspace;

namespace CentralConfigGenerator.Core.Tests.Workspace;

public class PackageUpdateServiceTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ccg-update");
    private static readonly string PropsPath = Path.Combine(Root, "Directory.Packages.props");

    private const string Props = """
        <Project>
          <PropertyGroup>
            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
          </PropertyGroup>
          <ItemGroup>
            <PackageVersion Include="Alpha" Version="1.0.0" />
            <PackageVersion Include="Beta" Version="1.0.0" />
            <PackageVersion Include="Gamma" Version="1.0.0" />
            <PackageVersion Include="Delta" Version="1.0.0" />
          </ItemGroup>
        </Project>
        """;

    private static FakeNuGetFeed Feed() =>
        new FakeNuGetFeed()
            .Add("Alpha", ["1.0.0", "2.0.0"])
            .Add("Beta", ["1.0.0", "2.0.0"])
            .Add("Gamma", ["1.0.0", "2.0.0"])
            .Add("Delta", ["1.0.0", "2.0.0"]);

    [Fact]
    public async Task UpdateAsync_ShouldReportCandidatesWithoutWritingOnDryRun()
    {
        var (service, fileSystem, _) = Create(_ => true);

        var result = await service.UpdateAsync(Options() with { DryRun = true });

        result.Candidates.Count.ShouldBe(4);
        result.Applied.ShouldBeEmpty();
        fileSystem.ReadText(PropsPath).ShouldBe(Props);
    }

    [Fact]
    public async Task UpdateAsync_ShouldApplyEverythingWhenTestsPass()
    {
        var (service, fileSystem, cli) = Create(_ => true);

        var result = await service.UpdateAsync(Options());

        result.Applied.Count.ShouldBe(4);
        result.TestsPassed.ShouldBeTrue();
        result.RolledBack.ShouldBeFalse();
        fileSystem.ReadText(PropsPath).ShouldContain("Include=\"Alpha\" Version=\"2.0.0\"");
        cli.TestCount.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRollEverythingBackWhenTestsFail()
    {
        var (service, fileSystem, _) = Create(_ => false);

        var result = await service.UpdateAsync(Options());

        result.Applied.ShouldBeEmpty();
        result.RolledBack.ShouldBeTrue();
        result.HeldBack.Count.ShouldBe(4);
        fileSystem.ReadText(PropsPath).ShouldBe(Props);
    }

    [Fact]
    public async Task UpdateAsync_ShouldKeepTheGreenSubsetWhenBisecting()
    {
        // Gamma is the poison pill: any set containing it fails.
        var (service, fileSystem, _) = Create(versions => versions["Gamma"] == "1.0.0");

        var result = await service.UpdateAsync(Options() with { Bisect = true });

        result.TestsPassed.ShouldBeTrue();
        result.RolledBack.ShouldBeFalse();
        result.Applied.ShouldNotBeEmpty();
        result.Applied.ShouldNotContain(c => c.PackageId == "Gamma");
        result.HeldBack.ShouldContain(c => c.PackageId == "Gamma");
        fileSystem.ReadText(PropsPath).ShouldContain("Include=\"Gamma\" Version=\"1.0.0\"");
    }

    [Fact]
    public async Task UpdateAsync_ShouldNameWhatItHeldBack()
    {
        var (service, _, _) = Create(versions => versions["Gamma"] == "1.0.0");

        var result = await service.UpdateAsync(Options() with { Bisect = true });

        result.Message.ShouldNotBeNull().ShouldContain("Gamma");
    }

    [Fact]
    public async Task UpdateAsync_ShouldRollBackWhenNoSubsetPasses()
    {
        var (service, fileSystem, _) = Create(_ => false);

        var result = await service.UpdateAsync(Options() with { Bisect = true });

        result.RolledBack.ShouldBeTrue();
        fileSystem.ReadText(PropsPath).ShouldBe(Props);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRespectTheBisectBudget()
    {
        var (service, _, cli) = Create(versions => versions["Gamma"] == "1.0.0");

        await service.UpdateAsync(Options() with { Bisect = true, BisectBudget = 3 });

        cli.TestCount.ShouldBeLessThanOrEqualTo(4);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRestrictToTheOnlyList()
    {
        var (service, _, _) = Create(_ => true);

        var result = await service.UpdateAsync(Options() with { Only = ["Alpha"] });

        result.Candidates.ShouldHaveSingleItem();
        result.Candidates[0].PackageId.ShouldBe("Alpha");
    }

    [Fact]
    public async Task UpdateAsync_ShouldSkipTestsWhenAsked()
    {
        var (service, fileSystem, cli) = Create(_ => false);

        var result = await service.UpdateAsync(Options() with { SkipTests = true });

        cli.TestCount.ShouldBe(0);
        result.Applied.Count.ShouldBe(4);
        fileSystem.ReadText(PropsPath).ShouldContain("2.0.0");
    }

    [Fact]
    public async Task UpdateAsync_ShouldExplainWhenThereIsNoPropsFile()
    {
        var fileSystem = new InMemoryFileSystem().AddDirectory(Root);
        var service = Build(fileSystem, new FakeDotNetCli(_ => true, () => string.Empty));

        var result = await service.UpdateAsync(Options());

        result.Candidates.ShouldBeEmpty();
        result.Message.ShouldNotBeNull().ShouldContain("No Directory.Packages.props");
    }

    [Fact]
    public async Task UpdateAsync_ShouldSayWhenEverythingIsCurrent()
    {
        var fileSystem = new InMemoryFileSystem().AddFile(PropsPath, Props);
        var feed = new FakeNuGetFeed()
            .Add("Alpha", ["1.0.0"])
            .Add("Beta", ["1.0.0"])
            .Add("Gamma", ["1.0.0"])
            .Add("Delta", ["1.0.0"]);

        var service = new PackageUpdateService(
            new ProjectDiscoveryService(fileSystem),
            fileSystem,
            feed,
            new FakeDotNetCli(_ => true, () => fileSystem.ReadText(PropsPath)),
            new BackupService(fileSystem)
        );

        var result = await service.UpdateAsync(Options());

        result.Message.ShouldNotBeNull().ShouldContain("up to date");
    }

    private static PackageUpdateOptions Options() =>
        new()
        {
            RootDirectory = Root,
            PackagesPropsPath = PropsPath,
            BackupDirectory = Root,
        };

    private static (PackageUpdateService Service, InMemoryFileSystem FileSystem, FakeDotNetCli Cli) Create(
        Func<IReadOnlyDictionary<string, string>, bool> testsPass
    )
    {
        var fileSystem = new InMemoryFileSystem().AddFile(PropsPath, Props);
        var cli = new FakeDotNetCli(testsPass, () => fileSystem.ReadText(PropsPath));

        return (Build(fileSystem, cli), fileSystem, cli);
    }

    private static PackageUpdateService Build(InMemoryFileSystem fileSystem, FakeDotNetCli cli) =>
        new(
            new ProjectDiscoveryService(fileSystem),
            fileSystem,
            Feed(),
            cli,
            new BackupService(fileSystem)
        );
}
