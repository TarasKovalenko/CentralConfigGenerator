using CentralConfigGenerator.Core.Migration;
using CentralConfigGenerator.Core.Tests.TestSupport;
using CentralConfigGenerator.Core.Workspace;

namespace CentralConfigGenerator.Core.Tests.Workspace;

public class BatchServiceTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ccg-batch");

    [Fact]
    public void DiscoverWorkspaces_ShouldReturnOneDirectoryPerSolution()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "repo-a", "A.sln"), "sln")
            .AddFile(Path.Combine(Root, "repo-b", "B.slnx"), "<Solution />");

        var workspaces = new BatchService(fileSystem).DiscoverWorkspaces(
            new BatchOptions { RootDirectory = Root }
        );

        workspaces.Count.ShouldBe(2);
    }

    [Fact]
    public void DiscoverWorkspaces_ShouldFallBackToProjectDirectories()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "repo-a", "A.csproj"), Sample.AppProject);

        var workspaces = new BatchService(fileSystem).DiscoverWorkspaces(
            new BatchOptions { RootDirectory = Root }
        );

        workspaces.ShouldHaveSingleItem();
    }

    [Fact]
    public void DiscoverWorkspaces_ShouldRespectMaxDepth()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "a", "b", "c", "d", "Deep.sln"), "sln");

        var workspaces = new BatchService(fileSystem).DiscoverWorkspaces(
            new BatchOptions { RootDirectory = Root, MaxDepth = 2 }
        );

        workspaces.ShouldBeEmpty();
    }

    [Fact]
    public void DiscoverWorkspaces_ShouldSkipBuildOutput()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "repo", "A.sln"), "sln")
            .AddFile(Path.Combine(Root, "repo", "bin", "Copy.sln"), "sln");

        var workspaces = new BatchService(fileSystem).DiscoverWorkspaces(
            new BatchOptions { RootDirectory = Root }
        );

        workspaces.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RunAsync_ShouldProcessEveryWorkspace()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "repo-a", "A.sln"), "sln")
            .AddFile(Path.Combine(Root, "repo-b", "B.sln"), "sln");

        var result = await new BatchService(fileSystem).RunAsync(
            new BatchOptions { RootDirectory = Root },
            (_, _) => Task.FromResult(EmptyPlan())
        );

        result.SucceededCount.ShouldBe(2);
        result.FailedCount.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ShouldStopAtTheFirstFailureByDefault()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "repo-a", "A.sln"), "sln")
            .AddFile(Path.Combine(Root, "repo-b", "B.sln"), "sln");

        var result = await new BatchService(fileSystem).RunAsync(
            new BatchOptions { RootDirectory = Root },
            (_, _) => throw new InvalidOperationException("boom")
        );

        result.Items.Count.ShouldBe(1);
        result.Items[0].Error.ShouldBe("boom");
    }

    [Fact]
    public async Task RunAsync_ShouldKeepGoingWhenContinueOnErrorIsSet()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "repo-a", "A.sln"), "sln")
            .AddFile(Path.Combine(Root, "repo-b", "B.sln"), "sln");

        var result = await new BatchService(fileSystem).RunAsync(
            new BatchOptions { RootDirectory = Root, ContinueOnError = true },
            (_, _) => throw new InvalidOperationException("boom")
        );

        result.FailedCount.ShouldBe(2);
    }

    private static MigrationPlan EmptyPlan() =>
        new()
        {
            Changes = [],
            ResolvedVersions = new Dictionary<string, string>(),
            Conflicts = [],
            Findings = [],
            ProjectCount = 0,
        };
}
