using CentralConfigGenerator.Core.NuGet;

namespace CentralConfigGenerator.Core.Tests.Workspace;

public class PackageGraphServiceTests
{
    private const string SampleJson = """
        {
          "version": 1,
          "projects": [
            {
              "path": "/repo/src/App/App.csproj",
              "frameworks": [
                {
                  "framework": "net8.0",
                  "topLevelPackages": [
                    { "id": "Serilog", "requestedVersion": "3.1.1", "resolvedVersion": "3.1.1" }
                  ],
                  "transitivePackages": [
                    { "id": "System.Text.Json", "resolvedVersion": "8.0.0" }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Parse_ShouldReadDirectAndTransitivePackages()
    {
        var graph = PackageGraphService.Parse(SampleJson);

        graph.IsAvailable.ShouldBeTrue();
        graph.Direct.Count().ShouldBe(1);
        graph.Transitive.Count().ShouldBe(1);
        graph.Packages[0].TargetFramework.ShouldBe("net8.0");
    }

    [Fact]
    public void Parse_ShouldToleratePrefixNoiseBeforeTheJson()
    {
        var graph = PackageGraphService.Parse("Restoring...\n" + SampleJson);

        graph.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public void Parse_ShouldReportAnErrorForNonJson()
    {
        var graph = PackageGraphService.Parse("no json here");

        graph.IsAvailable.ShouldBeFalse();
        graph.Error.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_ShouldReportAnErrorForMalformedJson()
    {
        var graph = PackageGraphService.Parse("{ \"projects\": [ ");

        graph.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void DivergentPackages_ShouldFindVersionSplits()
    {
        var graph = new PackageGraph
        {
            IsAvailable = true,
            Packages =
            [
                Package("A", "1.0.0", "One.csproj"),
                Package("A", "2.0.0", "Two.csproj"),
                Package("B", "1.0.0", "One.csproj"),
            ],
        };

        graph.DivergentPackages.Select(g => g.Key).ShouldBe(["A"]);
    }

    [Fact]
    public void Signature_ShouldKeyByProjectFrameworkAndPackage()
    {
        var graph = new PackageGraph
        {
            IsAvailable = true,
            Packages = [Package("A", "1.0.0", "/repo/One.csproj")],
        };

        var signature = graph.Signature();

        signature.ShouldContainKey("One.csproj|net8.0|A");
        signature["One.csproj|net8.0|A"].ShouldBe("1.0.0");
    }

    private static ResolvedPackage Package(string id, string version, string project) =>
        new()
        {
            PackageId = id,
            ResolvedVersion = version,
            ProjectPath = project,
            TargetFramework = "net8.0",
        };
}
