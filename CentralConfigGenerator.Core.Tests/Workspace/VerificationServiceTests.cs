using CentralConfigGenerator.Core.NuGet;
using CentralConfigGenerator.Core.Services;
using CentralConfigGenerator.Core.Workspace;

namespace CentralConfigGenerator.Core.Tests.Workspace;

public class VerificationServiceTests
{
    private readonly VerificationService _service = new(
        new TestSupport.FakePackageGraphService(PackageGraph.Empty),
        new DotNetCliService()
    );

    [Fact]
    public void Compare_ShouldReportNoDriftForIdenticalGraphs()
    {
        var graph = Graph(("A", "1.0.0"));

        var result = _service.Compare(graph, graph);

        result.IsClean.ShouldBeTrue();
        result.GraphAvailable.ShouldBeTrue();
    }

    [Fact]
    public void Compare_ShouldReportChangedVersions()
    {
        var result = _service.Compare(Graph(("A", "1.0.0")), Graph(("A", "2.0.0")));

        result.Drift.ShouldHaveSingleItem();
        result.Drift[0].Before.ShouldBe("1.0.0");
        result.Drift[0].After.ShouldBe("2.0.0");
    }

    [Fact]
    public void Compare_ShouldReportAdditions()
    {
        var result = _service.Compare(Graph(("A", "1.0.0")), Graph(("A", "1.0.0"), ("B", "1.0.0")));

        result.Drift.ShouldHaveSingleItem();
        result.Drift[0].IsAddition.ShouldBeTrue();
    }

    [Fact]
    public void Compare_ShouldReportRemovals()
    {
        var result = _service.Compare(Graph(("A", "1.0.0"), ("B", "1.0.0")), Graph(("A", "1.0.0")));

        result.Drift.ShouldHaveSingleItem();
        result.Drift[0].IsRemoval.ShouldBeTrue();
    }

    [Fact]
    public void Compare_ShouldSurfaceUnavailableGraphs()
    {
        var result = _service.Compare(PackageGraph.Empty, Graph(("A", "1.0.0")));

        result.GraphAvailable.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
    }

    private static PackageGraph Graph(params (string Id, string Version)[] packages) =>
        new()
        {
            IsAvailable = true,
            Packages = packages
                .Select(p => new ResolvedPackage
                {
                    PackageId = p.Id,
                    ResolvedVersion = p.Version,
                    ProjectPath = "/repo/App.csproj",
                    TargetFramework = "net8.0",
                })
                .ToList(),
        };
}
