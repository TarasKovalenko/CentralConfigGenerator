using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Core.Tests.TestSupport;

namespace CentralConfigGenerator.Core.Tests.Migration;

public class PackagesPropsEditorTests
{
    [Fact]
    public void SetVersion_ShouldUpdateAnExistingEntry()
    {
        var updated = PackagesPropsEditor.SetVersion(Sample.PackagesProps, "Serilog", "4.0.0");

        updated.ShouldNotBeNull();
        updated.ShouldContain("Include=\"Serilog\" Version=\"4.0.0\"");
    }

    [Fact]
    public void SetVersion_ShouldReturnNullWhenNothingChanges() =>
        PackagesPropsEditor.SetVersion(Sample.PackagesProps, "Serilog", "3.1.1").ShouldBeNull();

    [Fact]
    public void SetVersion_ShouldAppendANewEntry()
    {
        var updated = PackagesPropsEditor.SetVersion(Sample.PackagesProps, "Polly", "8.4.1");

        updated.ShouldNotBeNull();
        updated.ShouldContain("<PackageVersion Include=\"Polly\" Version=\"8.4.1\" />");
        updated.ShouldContain("Serilog");
    }

    [Fact]
    public void RemoveEntry_ShouldDeleteThePackageVersion()
    {
        var updated = PackagesPropsEditor.RemoveEntry(Sample.PackagesProps, "Serilog");

        updated.ShouldNotBeNull();
        updated.ShouldNotContain("Serilog");
        updated.ShouldContain("Newtonsoft.Json");
    }

    [Fact]
    public void RemoveEntry_ShouldReturnNullForAnUnknownPackage() =>
        PackagesPropsEditor.RemoveEntry(Sample.PackagesProps, "Nope").ShouldBeNull();

    [Fact]
    public void NormalizeCasing_ShouldRewriteTheIncludeAttribute()
    {
        var content = Sample.PackagesProps.Replace("Newtonsoft.Json", "newtonsoft.json");

        var updated = PackagesPropsEditor.NormalizeCasing(content, "Newtonsoft.Json");

        updated.ShouldNotBeNull();
        updated.ShouldContain("Include=\"Newtonsoft.Json\"");
    }

    [Fact]
    public void DeduplicateEntries_ShouldKeepOnlyTheFirstSpelling()
    {
        var content = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Serilog" Version="3.1.1" />
                <PackageVersion Include="serilog" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """;

        var updated = PackagesPropsEditor.DeduplicateEntries(content);

        updated.ShouldNotBeNull();
        updated.ShouldContain("Version=\"3.1.1\"");
        updated.ShouldNotContain("2.0.0");
    }
}
