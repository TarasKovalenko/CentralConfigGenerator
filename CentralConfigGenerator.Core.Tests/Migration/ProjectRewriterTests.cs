using CentralConfigGenerator.Core.Migration;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Tests.TestSupport;

namespace CentralConfigGenerator.Core.Tests.Migration;

public class ProjectRewriterTests
{
    [Fact]
    public void StripVersions_ShouldRemoveAttributeVersions()
    {
        var updated = ProjectRewriter.StripVersions(Project(Sample.AppProject), out var touched);

        updated.ShouldNotBeNull();
        updated.ShouldNotContain("Version=");
        updated.ShouldContain("<PackageReference Include=\"Newtonsoft.Json\" />");
        touched.Count.ShouldBe(2);
    }

    [Fact]
    public void StripVersions_ShouldCollapseChildElementVersions()
    {
        var updated = ProjectRewriter.StripVersions(Project(Sample.LibProject), out _);

        updated.ShouldNotBeNull();
        updated.ShouldNotContain("<Version>");
        updated.ShouldContain("<PackageReference Include=\"Serilog\" />");
    }

    [Fact]
    public void StripVersions_ShouldPreserveEverythingElse()
    {
        var updated = ProjectRewriter.StripVersions(Project(Sample.AppProject), out _);

        updated.ShouldNotBeNull();
        updated.ShouldContain("<OutputType>Exe</OutputType>");
        updated.ShouldContain("<TargetFramework>net8.0</TargetFramework>");
    }

    [Fact]
    public void StripVersions_ShouldReturnNullWhenThereIsNothingToDo()
    {
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" />
              </ItemGroup>
            </Project>
            """;

        ProjectRewriter.StripVersions(Project(content), out _).ShouldBeNull();
    }

    [Fact]
    public void StripVersion_ShouldOnlyTouchTheNamedPackage()
    {
        var updated = ProjectRewriter.StripVersion(Project(Sample.AppProject), "Serilog");

        updated.ShouldNotBeNull();
        updated.ShouldContain("Include=\"Newtonsoft.Json\" Version=\"13.0.1\"");
        updated.ShouldContain("<PackageReference Include=\"Serilog\" />");
    }

    [Fact]
    public void RestoreVersions_ShouldInlineCentralVersionsAfterInclude()
    {
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" PrivateAssets="all" />
              </ItemGroup>
            </Project>
            """;

        var updated = ProjectRewriter.RestoreVersions(
            Project(content),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Serilog"] = "3.1.1" },
            out var unresolved
        );

        updated.ShouldNotBeNull();
        updated.ShouldContain("Include=\"Serilog\" Version=\"3.1.1\" PrivateAssets=\"all\"");
        unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void RestoreVersions_ShouldReportPackagesWithNoCentralEntry()
    {
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Unknown" />
              </ItemGroup>
            </Project>
            """;

        ProjectRewriter.RestoreVersions(
            Project(content),
            new Dictionary<string, string>(),
            out var unresolved
        );

        unresolved.ShouldBe(["Unknown"]);
    }

    [Fact]
    public void RemoveProperties_ShouldDeleteMatchingValuesOnly()
    {
        var updated = ProjectRewriter.RemoveProperties(
            Project(Sample.AppProject),
            ["Nullable", "TargetFramework"],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nullable"] = "enable",
                ["TargetFramework"] = "net9.0",
            }
        );

        updated.ShouldNotBeNull();
        updated.ShouldNotContain("<Nullable>");
        // net8.0 does not match the expected net9.0, so it must stay.
        updated.ShouldContain("<TargetFramework>net8.0</TargetFramework>");
    }

    [Fact]
    public void RemoveProperties_ShouldLeaveConditionedGroupsAlone()
    {
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup Condition="'$(Configuration)' == 'Release'">
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """;

        ProjectRewriter.RemoveProperties(Project(content), ["Nullable"]).ShouldBeNull();
    }

    [Fact]
    public void RemoveDuplicateReference_ShouldKeepTheFirstOccurrence()
    {
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="xunit" Version="2.9.3" />
                <PackageReference Include="xunit" Version="2.9.0" />
              </ItemGroup>
            </Project>
            """;

        var updated = ProjectRewriter.RemoveDuplicateReference(Project(content), "xunit");

        updated.ShouldNotBeNull();
        updated.Split("<PackageReference").Length.ShouldBe(2);
        updated.ShouldContain("2.9.3");
    }

    [Fact]
    public void RemoveDuplicateReference_ShouldDoNothingWhenThereIsOnlyOne() =>
        ProjectRewriter.RemoveDuplicateReference(Project(Sample.AppProject), "Serilog").ShouldBeNull();

    [Fact]
    public void NormalizeCasing_ShouldRewriteToCanonicalSpelling()
    {
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="newtonsoft.json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """;

        var updated = ProjectRewriter.NormalizeCasing(Project(content), "Newtonsoft.Json");

        updated.ShouldNotBeNull();
        updated.ShouldContain("Include=\"Newtonsoft.Json\"");
    }

    [Fact]
    public void NormalizeCasing_ShouldReturnNullWhenAlreadyCanonical() =>
        ProjectRewriter.NormalizeCasing(Project(Sample.AppProject), "Newtonsoft.Json").ShouldBeNull();

    private static ProjectFile Project(string content) =>
        new() { Path = Path.Combine(Path.GetTempPath(), "Test.csproj"), Content = content };
}
