using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Xml;

namespace CentralConfigGenerator.Core.Tests.Xml;

public class PackageReferenceReaderTests
{
    [Fact]
    public void Read_ShouldPickUpAttributeVersions()
    {
        var project = Project("""
            <ItemGroup>
              <PackageReference Include="Serilog" Version="3.1.1" />
            </ItemGroup>
            """);

        var references = PackageReferenceReader.Read(project);

        references.Count.ShouldBe(1);
        references[0].PackageId.ShouldBe("Serilog");
        references[0].Version.ShouldBe("3.1.1");
        references[0].VersionIsElement.ShouldBeFalse();
    }

    [Fact]
    public void Read_ShouldPickUpChildElementVersions()
    {
        var project = Project("""
            <ItemGroup>
              <PackageReference Include="Serilog">
                <Version>2.12.0</Version>
              </PackageReference>
            </ItemGroup>
            """);

        var references = PackageReferenceReader.Read(project);

        references[0].Version.ShouldBe("2.12.0");
        references[0].VersionIsElement.ShouldBeTrue();
    }

    [Fact]
    public void Read_ShouldPickUpGlobalPackageReferences()
    {
        var project = Project("""
            <ItemGroup>
              <GlobalPackageReference Include="Roslynator.Analyzers" Version="4.12.0" />
            </ItemGroup>
            """);

        var references = PackageReferenceReader.Read(project);

        references[0].IsGlobal.ShouldBeTrue();
    }

    [Fact]
    public void Read_ShouldCaptureInheritedConditions()
    {
        var project = Project("""
            <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
              <PackageReference Include="Serilog" Version="3.1.1" />
            </ItemGroup>
            """);

        PackageReferenceReader.Read(project)[0].Condition.ShouldNotBeNull().ShouldContain("net8.0");
    }

    [Fact]
    public void Read_ShouldReportPrivateAssets()
    {
        var project = Project("""
            <ItemGroup>
              <PackageReference Include="StyleCop" Version="1.2.0" PrivateAssets="all" />
            </ItemGroup>
            """);

        PackageReferenceReader.Read(project)[0].IsPrivateAssets.ShouldBeTrue();
    }

    [Fact]
    public void Read_ShouldReturnEmptyForUnparseableXml()
    {
        var project = new ProjectFile { Path = "Broken.csproj", Content = "<Project>" };

        PackageReferenceReader.Read(project).ShouldBeEmpty();
    }

    [Fact]
    public void Read_ShouldTreatVersionlessReferencesAsNullVersion()
    {
        var project = Project("""
            <ItemGroup>
              <PackageReference Include="Serilog" />
            </ItemGroup>
            """);

        PackageReferenceReader.Read(project)[0].Version.ShouldBeNull();
    }

    [Fact]
    public void ReadPackageVersions_ShouldReadCentralEntries()
    {
        var document = MsBuildXml.Parse(TestSupport.Sample.PackagesProps);

        var versions = PackageReferenceReader.ReadPackageVersions(document);

        versions.Count.ShouldBe(2);
        versions["newtonsoft.json"].ShouldBe("13.0.3");
    }

    private static ProjectFile Project(string body) =>
        new()
        {
            Path = Path.Combine(Path.GetTempPath(), "Test.csproj"),
            Content = $"<Project Sdk=\"Microsoft.NET.Sdk\">\n{body}\n</Project>",
        };
}
