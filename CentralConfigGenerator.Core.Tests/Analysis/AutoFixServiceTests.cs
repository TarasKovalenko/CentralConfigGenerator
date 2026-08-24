using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.NuGet;
using CentralConfigGenerator.Core.Services;
using CentralConfigGenerator.Core.Tests.TestSupport;

namespace CentralConfigGenerator.Core.Tests.Analysis;

public class AutoFixServiceTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ccg-autofix");

    [Fact]
    public async Task PlanAsync_ShouldRemoveInlineVersionsUnderCentralManagement()
    {
        var project = Path.Combine(Root, "A.csproj");

        var fileSystem = new InMemoryFileSystem()
            .AddFile(project, Sample.AppProject)
            .AddFile(Path.Combine(Root, "Directory.Packages.props"), Sample.PackagesProps);

        var (service, context) = await CreateAsync(fileSystem);

        var result = await service.PlanAsync(
            context,
            context.References.Select(r => new Finding
            {
                RuleId = RuleIds.InlineVersionUnderCpm,
                Severity = Severity.High,
                Message = "inline",
                PackageId = r.PackageId,
                ProjectPath = r.ProjectPath,
                Fix = FixKind.RemoveInlineVersion,
            }).ToList()
        );

        result.Changes.ShouldHaveSingleItem();
        result.Changes[0].NewContent.ShouldNotContain("Version=");
        result.Fixed.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PlanAsync_ShouldSetCentralVersions()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "A.csproj"), Sample.AppProject)
            .AddFile(Path.Combine(Root, "Directory.Packages.props"), Sample.PackagesProps);

        var (service, context) = await CreateAsync(fileSystem);

        var result = await service.PlanAsync(
            context,
            [
                new Finding
                {
                    RuleId = RuleIds.OutdatedPackage,
                    Severity = Severity.Low,
                    Message = "outdated",
                    PackageId = "Serilog",
                    Fix = FixKind.SetCentralVersion,
                    FixValue = "4.0.0",
                },
            ]
        );

        result.Changes.ShouldHaveSingleItem();
        result.Changes[0].NewContent.ShouldContain("Version=\"4.0.0\"");
    }

    [Fact]
    public async Task PlanAsync_ShouldStackSeveralFixesOnOneFile()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "A.csproj"), Sample.AppProject)
            .AddFile(Path.Combine(Root, "Directory.Packages.props"), Sample.PackagesProps);

        var (service, context) = await CreateAsync(fileSystem);

        var result = await service.PlanAsync(
            context,
            [
                Fix("Serilog", "4.0.0"),
                Fix("Newtonsoft.Json", "14.0.0"),
            ]
        );

        result.Changes.ShouldHaveSingleItem();
        result.Changes[0].NewContent.ShouldContain("4.0.0");
        result.Changes[0].NewContent.ShouldContain("14.0.0");

        static Finding Fix(string packageId, string version) =>
            new()
            {
                RuleId = RuleIds.OutdatedPackage,
                Severity = Severity.Low,
                Message = "outdated",
                PackageId = packageId,
                Fix = FixKind.SetCentralVersion,
                FixValue = version,
            };
    }

    [Fact]
    public async Task PlanAsync_ShouldSkipFindingsThatAreNotAutoFixable()
    {
        var fileSystem = new InMemoryFileSystem().AddFile(Path.Combine(Root, "A.csproj"), Sample.AppProject);

        var (service, context) = await CreateAsync(fileSystem);

        var result = await service.PlanAsync(
            context,
            [
                new Finding
                {
                    RuleId = RuleIds.FloatingVersion,
                    Severity = Severity.Moderate,
                    Message = "floating",
                },
            ]
        );

        result.Skipped.ShouldHaveSingleItem();
        result.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyAsync_ShouldWriteChangesAndBackThemUp()
    {
        var project = Path.Combine(Root, "A.csproj");

        var fileSystem = new InMemoryFileSystem()
            .AddFile(project, Sample.AppProject)
            .AddFile(Path.Combine(Root, "Directory.Packages.props"), Sample.PackagesProps);

        var (service, context) = await CreateAsync(fileSystem);

        var result = await service.PlanAsync(
            context,
            [
                new Finding
                {
                    RuleId = RuleIds.InlineVersionUnderCpm,
                    Severity = Severity.High,
                    Message = "inline",
                    PackageId = "Serilog",
                    ProjectPath = project,
                    Fix = FixKind.RemoveInlineVersion,
                },
            ]
        );

        var backup = await service.ApplyAsync(result, Root, Root, createBackup: true);

        fileSystem.ReadText(project).ShouldContain("<PackageReference Include=\"Serilog\" />");
        backup.ShouldNotBeNull();
    }

    private static async Task<(AutoFixService Service, AnalysisContext Context)> CreateAsync(
        InMemoryFileSystem fileSystem
    )
    {
        var discovery = new ProjectDiscoveryService(fileSystem);

        var engine = new AnalysisEngine(
            discovery,
            fileSystem,
            new FakeNuGetFeed(),
            new FakePackageGraphService(PackageGraph.Empty),
            new BaselineService(fileSystem)
        );

        var context = await engine.BuildContextAsync(new AnalysisOptions { RootDirectory = Root });
        var service = new AutoFixService(discovery, fileSystem, new BackupService(fileSystem));

        return (service, context);
    }
}
