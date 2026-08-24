using CentralConfigGenerator.Core.NuGet;
using CentralConfigGenerator.Core.Workspace;

namespace CentralConfigGenerator.Core.Tests.Workspace;

public class DependencyTreeServiceTests
{
    private static readonly PackageGraph Graph = new()
    {
        IsAvailable = true,
        Packages =
        [
            new ResolvedPackage
            {
                PackageId = "Serilog",
                ResolvedVersion = "3.1.1",
                RequestedVersion = "3.1.1",
                ProjectPath = "/repo/src/App/App.csproj",
                TargetFramework = "net8.0",
            },
            new ResolvedPackage
            {
                PackageId = "System.Text.Json",
                ResolvedVersion = "8.0.0",
                ProjectPath = "/repo/src/App/App.csproj",
                TargetFramework = "net8.0",
                IsTransitive = true,
            },
        ],
    };

    [Fact]
    public void Render_ShouldDrawProjectFrameworkAndPackages()
    {
        var tree = DependencyTreeService.Render(Graph, includeTransitive: true);

        tree.ShouldContain("App");
        tree.ShouldContain("net8.0");
        tree.ShouldContain("Serilog 3.1.1");
        tree.ShouldContain("System.Text.Json 8.0.0 (transitive)");
    }

    [Fact]
    public void Render_ShouldOmitTransitivePackagesWhenNotRequested()
    {
        var tree = DependencyTreeService.Render(Graph, includeTransitive: false);

        tree.ShouldNotContain("System.Text.Json");
    }

    [Fact]
    public void Render_ShouldNoteWhenTheResolvedVersionDiffersFromTheRequest()
    {
        var graph = Graph with
        {
            Packages =
            [
                Graph.Packages[0] with { RequestedVersion = "3.0.0", ResolvedVersion = "3.1.1" },
            ],
        };

        DependencyTreeService.Render(graph, includeTransitive: false)
            .ShouldContain("(requested 3.0.0)");
    }

    [Fact]
    public async Task RenderAsync_ShouldExplainWhenTheGraphIsUnavailable()
    {
        var service = new DependencyTreeService(
            new TestSupport.FakePackageGraphService(
                PackageGraph.Empty with { Error = "restore failed" }
            )
        );

        var output = await service.RenderAsync("/repo", "/repo", includeTransitive: false);

        output.ShouldContain("Dependency graph unavailable");
        output.ShouldContain("restore failed");
    }
}
