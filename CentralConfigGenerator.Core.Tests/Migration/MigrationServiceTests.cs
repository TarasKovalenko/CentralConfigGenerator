using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.Migration;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Services;
using CentralConfigGenerator.Core.Tests.TestSupport;

namespace CentralConfigGenerator.Core.Tests.Migration;

public class MigrationServiceTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ccg-migration");

    [Fact]
    public async Task PlanAsync_ShouldCreateThePropsFileAndStripEveryProject()
    {
        var (service, _) = Create(Workspace());

        var plan = await service.PlanAsync(Options());

        plan.ProjectCount.ShouldBe(2);
        plan.Changes.ShouldContain(c => c.Kind == FileChangeKind.Created);
        plan.Changes.Count(c => c.Kind == FileChangeKind.Modified).ShouldBe(2);
        plan.ResolvedVersions["Newtonsoft.Json"].ShouldBe("13.0.3");
    }

    [Fact]
    public async Task PlanAsync_ShouldNotWriteAnythingToDisk()
    {
        var fileSystem = Workspace();
        var (service, _) = Create(fileSystem);

        await service.PlanAsync(Options());

        fileSystem.FileExists(Path.Combine(Root, "Directory.Packages.props")).ShouldBeFalse();
    }

    [Fact]
    public async Task PlanAsync_ShouldRecordConflicts()
    {
        var (service, _) = Create(Workspace());

        var plan = await service.PlanAsync(Options());

        plan.Conflicts.ShouldContain(c => c.PackageId == "Newtonsoft.Json");
        plan.Conflicts.First(c => c.PackageId == "Newtonsoft.Json").ResolvedVersion.ShouldBe("13.0.3");
    }

    [Fact]
    public async Task PlanAsync_ShouldRespectTheLowestStrategy()
    {
        var (service, _) = Create(Workspace());

        var plan = await service.PlanAsync(
            Options() with { ConflictStrategy = VersionResolutionStrategy.Lowest }
        );

        plan.ResolvedVersions["Newtonsoft.Json"].ShouldBe("13.0.1");
    }

    [Fact]
    public async Task PlanAsync_ShouldThrowForTheFailStrategy()
    {
        var (service, _) = Create(Workspace());

        await Should.ThrowAsync<VersionConflictException>(() =>
            service.PlanAsync(
                Options() with
                {
                    ConflictStrategy = VersionResolutionStrategy.Fail,
                    FailOnConflict = true,
                }
            )
        );
    }

    [Fact]
    public async Task PlanAsync_ShouldUseTheCallerSuppliedResolution()
    {
        var (service, _) = Create(Workspace());

        var plan = await service.PlanAsync(Options(), _ => "99.0.0");

        plan.ResolvedVersions["Newtonsoft.Json"].ShouldBe("99.0.0");
    }

    [Fact]
    public async Task PlanAsync_ShouldKeepInlineVersionsWhenAsked()
    {
        var (service, _) = Create(Workspace());

        var plan = await service.PlanAsync(Options() with { KeepInlineVersions = true });

        plan.Changes.Count.ShouldBe(1);
        plan.Changes[0].Kind.ShouldBe(FileChangeKind.Created);
    }

    [Fact]
    public async Task PlanAsync_ShouldMergeIntoAnExistingPropsFile()
    {
        var fileSystem = Workspace()
            .AddFile(Path.Combine(Root, "Directory.Packages.props"), Sample.PackagesProps);

        var (service, _) = Create(fileSystem);

        var plan = await service.PlanAsync(Options() with { Merge = true });

        var change = plan.Changes.First(c => c.Path.EndsWith("Directory.Packages.props", StringComparison.Ordinal));
        change.Kind.ShouldBe(FileChangeKind.Modified);
        change.NewContent.ShouldContain("Newtonsoft.Json");
    }

    [Fact]
    public async Task PlanAsync_ShouldAlsoHoistPropertiesWhenUnifyingIsOn()
    {
        var (service, _) = Create(Workspace());

        var plan = await service.PlanAsync(Options() with { UnifyProperties = true });

        plan.Changes.ShouldContain(c => c.Path.EndsWith("Directory.Build.props", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanAsync_ShouldReturnAnEmptyPlanWhenNoProjectsExist()
    {
        var (service, _) = Create(new InMemoryFileSystem().AddDirectory(Root));

        var plan = await service.PlanAsync(Options());

        plan.ProjectCount.ShouldBe(0);
        plan.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyAsync_ShouldWriteEveryChangeAndTakeABackup()
    {
        var fileSystem = Workspace();
        var (service, _) = Create(fileSystem);

        var plan = await service.PlanAsync(Options());
        var backup = await service.ApplyAsync(plan, Options());

        fileSystem.FileExists(Path.Combine(Root, "Directory.Packages.props")).ShouldBeTrue();
        fileSystem.ReadText(Path.Combine(Root, "a", "A.csproj")).ShouldNotContain("Version=");
        backup.ShouldNotBeNull();
        backup.Files.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ApplyAsync_ShouldSkipTheBackupWhenDisabled()
    {
        var (service, _) = Create(Workspace());

        var plan = await service.PlanAsync(Options());
        var backup = await service.ApplyAsync(plan, Options() with { CreateBackup = false });

        backup.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_ShouldRewriteLineEndingsWhenRequested()
    {
        var fileSystem = new InMemoryFileSystem().AddFile(
            Path.Combine(Root, "a", "A.csproj"),
            System.Text.Encoding.UTF8.GetBytes(
                "<Project>\r\n  <ItemGroup>\r\n    <PackageReference Include=\"Serilog\" Version=\"3.1.1\" />\r\n  </ItemGroup>\r\n</Project>\r\n"
            )
        );

        var (service, _) = Create(fileSystem);
        var options = Options() with { LineEnding = LineEndingStyle.Lf, CreateBackup = false };

        var plan = await service.PlanAsync(options);
        await service.ApplyAsync(plan, options);

        var written = System.Text.Encoding.UTF8.GetString(
            fileSystem.ReadBytes(Path.Combine(Root, "a", "A.csproj"))
        );

        written.ShouldNotContain("\r\n");
    }

    [Fact]
    public async Task PlanRevertAsync_ShouldInlineVersionsAndDeleteThePropsFile()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(
                Path.Combine(Root, "a", "A.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Serilog" />
                  </ItemGroup>
                </Project>
                """
            )
            .AddFile(Path.Combine(Root, "Directory.Packages.props"), Sample.PackagesProps);

        var (service, _) = Create(fileSystem);

        var plan = await service.PlanRevertAsync(Options());

        plan.Changes.ShouldContain(c => c.Kind == FileChangeKind.Deleted);
        plan.Changes.First(c => c.Kind == FileChangeKind.Modified).NewContent
            .ShouldContain("Version=\"3.1.1\"");
    }

    [Fact]
    public async Task PlanRevertAsync_ShouldReportPackagesWithNoCentralVersion()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(
                Path.Combine(Root, "a", "A.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Unknown" />
                  </ItemGroup>
                </Project>
                """
            )
            .AddFile(Path.Combine(Root, "Directory.Packages.props"), Sample.PackagesProps);

        var (service, _) = Create(fileSystem);

        var plan = await service.PlanRevertAsync(Options());

        plan.Findings.ShouldContain(f => f.RuleId == Core.Analysis.RuleIds.MissingCentralVersion);
    }

    private static MigrationOptions Options() =>
        new() { RootDirectory = Root, BackupDirectory = Root };

    private static InMemoryFileSystem Workspace() =>
        new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "a", "A.csproj"), Sample.AppProject)
            .AddFile(Path.Combine(Root, "b", "B.csproj"), Sample.LibProject);

    private static (MigrationService Service, InMemoryFileSystem FileSystem) Create(
        InMemoryFileSystem fileSystem
    )
    {
        var discovery = new ProjectDiscoveryService(fileSystem);
        var service = new MigrationService(
            discovery,
            fileSystem,
            new BackupService(fileSystem),
            new VersionSelector(new VersionConflictResolver())
        );

        return (service, fileSystem);
    }
}
