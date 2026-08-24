using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.NuGet;
using CentralConfigGenerator.Core.Tests.TestSupport;

namespace CentralConfigGenerator.Core.Tests.Analysis;

public class AnalysisEngineTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ccg-analysis");

    [Fact]
    public async Task AnalyzeAsync_ShouldReportVersionInconsistency()
    {
        var report = await RunAsync(WorkspaceWithTwoProjects());

        report.Findings.ShouldContain(f => f.RuleId == RuleIds.VersionInconsistency);
        report.Findings
            .First(f => f.RuleId == RuleIds.VersionInconsistency && f.PackageId == "Newtonsoft.Json")
            .FixValue.ShouldBe("13.0.3");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportCasingMismatches()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "a", "A.csproj"), Reference("Newtonsoft.Json", "13.0.3"))
            .AddFile(Path.Combine(Root, "b", "B.csproj"), Reference("newtonsoft.json", "13.0.3"));

        var report = await RunAsync(fileSystem);

        report.Findings.ShouldContain(f => f.RuleId == RuleIds.DuplicatePackageCasing);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportFloatingVersions()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "a", "A.csproj"), Reference("xunit", "2.9.*"));

        var report = await RunAsync(fileSystem);

        report.Findings.ShouldContain(f => f.RuleId == RuleIds.FloatingVersion);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportDuplicateReferencesInOneProject()
    {
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="xunit" Version="2.9.3" />
                <PackageReference Include="xunit" Version="2.9.0" />
              </ItemGroup>
            </Project>
            """;

        var fileSystem = new InMemoryFileSystem().AddFile(Path.Combine(Root, "A.csproj"), content);

        var report = await RunAsync(fileSystem);

        report.Findings.ShouldContain(f => f.RuleId == RuleIds.RedundantReference);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldNotFlagConditionedReferencesAsDuplicates()
    {
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
                <PackageReference Include="Polly" Version="8.0.0" />
              </ItemGroup>
              <ItemGroup Condition="'$(TargetFramework)' == 'net9.0'">
                <PackageReference Include="Polly" Version="8.4.1" />
              </ItemGroup>
            </Project>
            """;

        var fileSystem = new InMemoryFileSystem().AddFile(Path.Combine(Root, "A.csproj"), content);

        var report = await RunAsync(fileSystem);

        report.Findings.ShouldNotContain(f => f.RuleId == RuleIds.RedundantReference);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportInlineVersionsUnderCentralManagement()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "A.csproj"), Reference("Serilog", "2.0.0"))
            .AddFile(Path.Combine(Root, "Directory.Packages.props"), Sample.PackagesProps);

        var report = await RunAsync(fileSystem);

        var finding = report.Findings.First(f => f.RuleId == RuleIds.InlineVersionUnderCpm);
        finding.Severity.ShouldBe(Severity.High);
        finding.Fix.ShouldBe(FixKind.RemoveInlineVersion);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportOrphanedCentralEntries()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "A.csproj"), Reference("Serilog", null))
            .AddFile(Path.Combine(Root, "Directory.Packages.props"), Sample.PackagesProps);

        var report = await RunAsync(fileSystem);

        report.Findings.ShouldContain(f =>
            f.RuleId == RuleIds.OrphanedPackageVersion && f.PackageId == "Newtonsoft.Json"
        );
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportPackagesWithNoVersionAtAll()
    {
        var props = """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
            </Project>
            """;

        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "A.csproj"), Reference("Serilog", null))
            .AddFile(Path.Combine(Root, "Directory.Packages.props"), props);

        var report = await RunAsync(fileSystem);

        report.Findings.ShouldContain(f => f.RuleId == RuleIds.MissingCentralVersion);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldSkipNetworkRulesWhenTheyAreNotRequested()
    {
        var report = await RunAsync(WorkspaceWithTwoProjects());

        report.Findings.ShouldNotContain(f => f.RuleId == RuleIds.OutdatedPackage);
        report.Findings.ShouldNotContain(f => f.RuleId == RuleIds.SecurityVulnerability);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportVulnerabilitiesWhenAuditingIsOn()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "A.csproj"), Reference("Vulnerable.Pkg", "1.0.0"));

        var feed = new FakeNuGetFeed().Add(
            "Vulnerable.Pkg",
            ["1.0.0", "1.0.1"],
            vulnerabilities: [("1.0.0", Severity.Critical)]
        );

        var report = await RunAsync(fileSystem, feed, new AnalysisOptions { RootDirectory = Root, Audit = true });

        var finding = report.Findings.First(f => f.RuleId == RuleIds.SecurityVulnerability);
        finding.Severity.ShouldBe(Severity.Critical);
        finding.FixValue.ShouldBe("1.0.1");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportDeprecatedPackages()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "A.csproj"), Reference("Old.Pkg", "1.0.0"));

        var feed = new FakeNuGetFeed().Add("Old.Pkg", ["1.0.0"], deprecated: true, alternate: "New.Pkg");

        var report = await RunAsync(
            fileSystem,
            feed,
            new AnalysisOptions { RootDirectory = Root, Deprecated = true }
        );

        report.Findings
            .First(f => f.RuleId == RuleIds.DeprecatedPackage)
            .Recommendation.ShouldNotBeNull()
            .ShouldContain("New.Pkg");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldGradeOutdatedPackagesByHowFarBehindTheyAre()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "A.csproj"), Reference("Old.Pkg", "1.0.0"));

        var feed = new FakeNuGetFeed().Add("Old.Pkg", ["1.0.0", "3.0.0"]);

        var report = await RunAsync(
            fileSystem,
            feed,
            new AnalysisOptions { RootDirectory = Root, Outdated = true }
        );

        report.Findings.First(f => f.RuleId == RuleIds.OutdatedPackage).Severity
            .ShouldBe(Severity.Moderate);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldFlagCopyleftLicences()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "A.csproj"), Reference("Gpl.Pkg", "1.0.0"));

        var feed = new FakeNuGetFeed().Add("Gpl.Pkg", ["1.0.0"], license: "GPL-3.0-only");

        var report = await RunAsync(
            fileSystem,
            feed,
            new AnalysisOptions { RootDirectory = Root, Licenses = true }
        );

        report.Findings.First(f => f.RuleId == RuleIds.LicenseRisk).Severity.ShouldBe(Severity.High);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldHonourRuleSeverityOverrides()
    {
        var options = new AnalysisOptions
        {
            RootDirectory = Root,
            RuleOverrides = new Dictionary<string, Severity?>(StringComparer.OrdinalIgnoreCase)
            {
                [RuleIds.VersionInconsistency] = Severity.Critical,
            },
        };

        var report = await RunAsync(WorkspaceWithTwoProjects(), options: options);

        report.Findings
            .First(f => f.RuleId == RuleIds.VersionInconsistency)
            .Severity.ShouldBe(Severity.Critical);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldSkipDisabledRules()
    {
        var options = new AnalysisOptions
        {
            RootDirectory = Root,
            RuleOverrides = new Dictionary<string, Severity?>(StringComparer.OrdinalIgnoreCase)
            {
                [RuleIds.VersionInconsistency] = null,
            },
        };

        var report = await RunAsync(WorkspaceWithTwoProjects(), options: options);

        report.Findings.ShouldNotContain(f => f.RuleId == RuleIds.VersionInconsistency);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldIgnoreListedPackages()
    {
        var options = new AnalysisOptions { RootDirectory = Root, IgnorePackages = ["Newtonsoft.*"] };

        var report = await RunAsync(WorkspaceWithTwoProjects(), options: options);

        report.Findings.ShouldNotContain(f => f.PackageId == "Newtonsoft.Json");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldScoreACleanWorkspaceAt100()
    {
        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "A.csproj"), Reference("Serilog", "3.1.1"));

        var report = await RunAsync(fileSystem);

        report.Findings.ShouldBeEmpty();
        report.HealthScore.ShouldBe(100);
        report.Grade.ShouldBe("A");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReturnAnEmptyReportWhenThereAreNoProjects()
    {
        var report = await RunAsync(new InMemoryFileSystem().AddDirectory(Root));

        report.ProjectCount.ShouldBe(0);
        report.Findings.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportTransitiveDivergence()
    {
        var graph = new PackageGraph
        {
            IsAvailable = true,
            Packages =
            [
                new ResolvedPackage
                {
                    PackageId = "System.Text.Json",
                    ResolvedVersion = "8.0.0",
                    ProjectPath = "A.csproj",
                    TargetFramework = "net8.0",
                    IsTransitive = true,
                },
                new ResolvedPackage
                {
                    PackageId = "System.Text.Json",
                    ResolvedVersion = "9.0.0",
                    ProjectPath = "B.csproj",
                    TargetFramework = "net8.0",
                    IsTransitive = true,
                },
            ],
        };

        var fileSystem = new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "A.csproj"), Reference("Serilog", "3.1.1"));

        var engine = CreateEngine(fileSystem, new FakeNuGetFeed(), graph);
        var report = await engine.AnalyzeAsync(
            new AnalysisOptions { RootDirectory = Root, Transitive = true }
        );

        report.Findings.ShouldContain(f => f.RuleId == RuleIds.TransitiveConflict);
    }

    private static InMemoryFileSystem WorkspaceWithTwoProjects() =>
        new InMemoryFileSystem()
            .AddFile(Path.Combine(Root, "a", "A.csproj"), Reference("Newtonsoft.Json", "13.0.1"))
            .AddFile(Path.Combine(Root, "b", "B.csproj"), Reference("Newtonsoft.Json", "13.0.3"));

    private static string Reference(string packageId, string? version) =>
        $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="{packageId}"{(version is null ? string.Empty : $" Version=\"{version}\"")} />
          </ItemGroup>
        </Project>
        """;

    private static AnalysisEngine CreateEngine(
        InMemoryFileSystem fileSystem,
        INuGetFeedService feed,
        PackageGraph? graph = null
    ) =>
        new(
            new ProjectDiscoveryService(fileSystem),
            fileSystem,
            feed,
            new FakePackageGraphService(graph ?? PackageGraph.Empty),
            new BaselineService(fileSystem)
        );

    private static Task<AnalysisReport> RunAsync(
        InMemoryFileSystem fileSystem,
        INuGetFeedService? feed = null,
        AnalysisOptions? options = null
    ) =>
        CreateEngine(fileSystem, feed ?? new FakeNuGetFeed())
            .AnalyzeAsync(options ?? new AnalysisOptions { RootDirectory = Root });
}
