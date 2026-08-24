using CentralConfigGenerator.Core.Generators;

namespace CentralConfigGenerator.Core.Tests.Migration;

public class PackagesPropsWriterTests
{
    private static readonly Dictionary<string, string> Versions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Serilog"] = "3.1.1",
        ["Newtonsoft.Json"] = "13.0.3",
    };

    [Fact]
    public void Create_ShouldEnableCentralManagementAndSortEntries()
    {
        var content = PackagesPropsWriter.Create(Versions);

        content.ShouldContain("<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>");
        content.IndexOf("Newtonsoft.Json", StringComparison.Ordinal)
            .ShouldBeLessThan(content.IndexOf("Serilog", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_ShouldEmitTransitivePinningWhenRequested()
    {
        var content = PackagesPropsWriter.Create(
            Versions,
            new PackagesPropsOptions { TransitivePinning = true }
        );

        content.ShouldContain("<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>");
    }

    [Fact]
    public void Create_ShouldEmitGlobalPackageReferencesSeparately()
    {
        var content = PackagesPropsWriter.Create(
            Versions,
            new PackagesPropsOptions { GlobalPackages = ["Serilog"] }
        );

        content.ShouldContain("<GlobalPackageReference Include=\"Serilog\" Version=\"3.1.1\" />");
        content.ShouldNotContain("<PackageVersion Include=\"Serilog\"");
    }

    [Fact]
    public void Create_ShouldEscapeSpecialCharacters()
    {
        var content = PackagesPropsWriter.Create(
            new Dictionary<string, string> { ["A&B"] = "1.0.0" }
        );

        content.ShouldContain("A&amp;B");
    }

    [Fact]
    public void Merge_ShouldAddMissingEntriesAndKeepComments()
    {
        var existing = """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <!-- keep me -->
                <PackageVersion Include="Serilog" Version="3.0.0" />
              </ItemGroup>
            </Project>
            """;

        var merged = PackagesPropsWriter.Merge(existing, Versions);

        merged.ShouldContain("<!-- keep me -->");
        merged.ShouldContain("Include=\"Serilog\" Version=\"3.1.1\"");
        merged.ShouldContain("Newtonsoft.Json");
    }

    [Fact]
    public void Merge_ShouldAddTransitivePinningToAnExistingPropertyGroup()
    {
        var existing = """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
            </Project>
            """;

        var merged = PackagesPropsWriter.Merge(
            existing,
            Versions,
            new PackagesPropsOptions { TransitivePinning = true }
        );

        merged.ShouldContain("CentralPackageTransitivePinningEnabled");
    }

    [Fact]
    public void Merge_ShouldFallBackToCreateForUnparseableInput()
    {
        var merged = PackagesPropsWriter.Merge("<Project>", Versions);

        merged.ShouldContain("<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>");
    }

    [Fact]
    public void Merge_ShouldUpdateChildElementVersions()
    {
        var existing = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Serilog">
                  <Version>1.0.0</Version>
                </PackageVersion>
              </ItemGroup>
            </Project>
            """;

        var merged = PackagesPropsWriter.Merge(existing, Versions);

        merged.ShouldContain("<Version>3.1.1</Version>");
    }
}
