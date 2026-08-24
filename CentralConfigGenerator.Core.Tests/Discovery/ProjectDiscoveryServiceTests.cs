using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Tests.TestSupport;

namespace CentralConfigGenerator.Core.Tests.Discovery;

public class ProjectDiscoveryServiceTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ccg-discovery");

    [Fact]
    public async Task DiscoverAsync_ShouldFindProjectsRecursively()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "src", "App", "App.csproj"), Sample.AppProject)
            .AddFile(Path.Combine(Root, "src", "Lib", "Lib.fsproj"), Sample.LibProject);

        var service = new ProjectDiscoveryService(fileSystem);

        var projects = await service.DiscoverAsync(new DiscoveryOptions { RootDirectory = Root });

        projects.Count.ShouldBe(2);
        projects.Select(p => p.ResolvedKind).ShouldContain(ProjectKind.FSharp);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldSkipBinObjAndBackupDirectories()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "App.csproj"), Sample.AppProject)
            .AddFile(Path.Combine(Root, "bin", "Debug", "Ghost.csproj"), Sample.AppProject)
            .AddFile(Path.Combine(Root, "obj", "Ghost.csproj"), Sample.AppProject)
            .AddFile(Path.Combine(Root, "node_modules", "Ghost.csproj"), Sample.AppProject)
            .AddFile(
                Path.Combine(Root, ".centralconfig-backups", "20250101", "App.csproj"),
                Sample.AppProject
            );

        var service = new ProjectDiscoveryService(fileSystem);

        var projects = await service.DiscoverAsync(new DiscoveryOptions { RootDirectory = Root });

        projects.Count.ShouldBe(1);
        projects[0].FileName.ShouldBe("App.csproj");
    }

    [Fact]
    public async Task DiscoverAsync_ShouldHonourExcludeRegex()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "src", "App.csproj"), Sample.AppProject)
            .AddFile(Path.Combine(Root, "samples", "Demo.csproj"), Sample.AppProject);

        var service = new ProjectDiscoveryService(fileSystem);

        var projects = await service.DiscoverAsync(
            new DiscoveryOptions { RootDirectory = Root, ExcludePattern = "^samples$" }
        );

        projects.Count.ShouldBe(1);
        projects[0].FileName.ShouldBe("App.csproj");
    }

    [Fact]
    public async Task DiscoverAsync_ShouldRestrictToRequestedKinds()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "App.csproj"), Sample.AppProject)
            .AddFile(Path.Combine(Root, "Lib.fsproj"), Sample.LibProject);

        var service = new ProjectDiscoveryService(fileSystem);

        var projects = await service.DiscoverAsync(
            new DiscoveryOptions { RootDirectory = Root, IncludedKinds = [ProjectKind.CSharp] }
        );

        projects.Count.ShouldBe(1);
        projects[0].ResolvedKind.ShouldBe(ProjectKind.CSharp);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldFollowSolutionWhenGiven()
    {
        var solutionPath = Path.Combine(Root, "My.sln");
        var projectPath = Path.Combine(Root, "src", "App", "App.csproj");

        var fileSystem = new InMemoryFileSystem()
            .AddFile(projectPath, Sample.AppProject)
            .AddFile(Path.Combine(Root, "unlisted", "Other.csproj"), Sample.AppProject)
            .AddFile(
                solutionPath,
                """
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "src/App/App.csproj", "{G}"
                EndProject
                """
            );

        var service = new ProjectDiscoveryService(fileSystem);

        var projects = await service.DiscoverAsync(
            new DiscoveryOptions { RootDirectory = Root, SolutionPath = solutionPath }
        );

        projects.Count.ShouldBe(1);
        projects[0].Path.ShouldBe(projectPath);
    }

    [Fact]
    public async Task DiscoverAsync_ShouldReturnEmptyForMissingDirectory()
    {
        var service = new ProjectDiscoveryService(new InMemoryFileSystem());

        var projects = await service.DiscoverAsync(
            new DiscoveryOptions { RootDirectory = Path.Combine(Root, "nope") }
        );

        projects.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveAsync_ShouldPreserveTheOriginalFormat()
    {
        var path = Path.Combine(Root, "App.csproj");
        var fileSystem = new InMemoryFileSystem();
        fileSystem.AddFile(path, System.Text.Encoding.UTF8.GetBytes("<Project>\r\n</Project>\r\n"));

        var service = new ProjectDiscoveryService(fileSystem);
        var project = await service.LoadAsync(path);

        project.ShouldNotBeNull();
        await service.SaveAsync(project);

        System.Text.Encoding.UTF8.GetString(fileSystem.ReadBytes(path))
            .ShouldBe("<Project>\r\n</Project>\r\n");
    }
}
