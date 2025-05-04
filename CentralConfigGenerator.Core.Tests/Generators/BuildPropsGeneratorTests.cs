using CentralConfigGenerator.Core.Generators;
using System.Xml.Linq;
using CentralConfigGenerator.Core.Generators.Abstractions;

namespace CentralConfigGenerator.Core.Tests.Generators;

public class BuildPropsGeneratorTests
{
    private readonly IBuildPropsGenerator _generator = new BuildPropsGenerator();

    [Fact]
    public void GenerateBuildPropsContent_ShouldIncludeOnlySpecifiedProperties()
    {
        // Arrange
        var commonProperties = new Dictionary<string, string>
        {
            { "TargetFramework", "net9.0" },
            { "ImplicitUsings", "enable" },
            { "Nullable", "enable" },
            { "LangVersion", "latest" },
            { "Authors", "Test Author" }
        };

        // Act
        var result = _generator.GenerateBuildPropsContent(commonProperties);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();

        var xDoc = XDocument.Parse(result);
        var propertyGroup = xDoc.Root!.Element("PropertyGroup");

        propertyGroup.ShouldNotBeNull();

        // Should include the required properties
        propertyGroup.Element("TargetFramework").ShouldNotBeNull();
        propertyGroup.Element("TargetFramework")!.Value.ShouldBe("net9.0");

        propertyGroup.Element("ImplicitUsings").ShouldNotBeNull();
        propertyGroup.Element("ImplicitUsings")!.Value.ShouldBe("enable");

        propertyGroup.Element("Nullable").ShouldNotBeNull();
        propertyGroup.Element("Nullable")!.Value.ShouldBe("enable");

        // Should not include other properties
        propertyGroup.Element("LangVersion").ShouldBeNull();
        propertyGroup.Element("Authors").ShouldBeNull();
    }

    [Fact]
    public void GenerateBuildPropsContent_ShouldAddDefaultValuesForMissingRequiredProperties()
    {
        // Arrange
        var commonProperties = new Dictionary<string, string>
        {
            { "TargetFramework", "net9.0" },
            // ImplicitUsings and Nullable are missing
        };

        // Act
        var result = _generator.GenerateBuildPropsContent(commonProperties);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();

        var xDoc = XDocument.Parse(result);
        var propertyGroup = xDoc.Root!.Element("PropertyGroup");

        propertyGroup.ShouldNotBeNull();

        // TargetFramework from input
        propertyGroup.Element("TargetFramework").ShouldNotBeNull();
        propertyGroup.Element("TargetFramework")!.Value.ShouldBe("net9.0");

        // Default values for missing properties
        propertyGroup.Element("ImplicitUsings").ShouldNotBeNull();
        propertyGroup.Element("ImplicitUsings")!.Value.ShouldBe("enable");

        propertyGroup.Element("Nullable").ShouldNotBeNull();
        propertyGroup.Element("Nullable")!.Value.ShouldBe("enable");
    }

    [Fact]
    public void GenerateBuildPropsContent_ShouldNotAddTargetFrameworkDefaultIfMissing()
    {
        // Arrange
        var commonProperties = new Dictionary<string, string>
        {
            { "ImplicitUsings", "enable" },
            { "Nullable", "enable" },
            // TargetFramework is missing
        };

        // Act
        var result = _generator.GenerateBuildPropsContent(commonProperties);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();

        var xDoc = XDocument.Parse(result);
        var propertyGroup = xDoc.Root!.Element("PropertyGroup");

        propertyGroup.ShouldNotBeNull();

        // No default TargetFramework
        propertyGroup.Element("TargetFramework").ShouldBeNull();

        // Properties from input
        propertyGroup.Element("ImplicitUsings").ShouldNotBeNull();
        propertyGroup.Element("ImplicitUsings")!.Value.ShouldBe("enable");

        propertyGroup.Element("Nullable").ShouldNotBeNull();
        propertyGroup.Element("Nullable")!.Value.ShouldBe("enable");
    }

    [Fact]
    public void GenerateBuildPropsContent_ShouldReturnValidXml()
    {
        // Arrange
        var commonProperties = new Dictionary<string, string>
        {
            { "TargetFramework", "net9.0" },
            { "ImplicitUsings", "enable" },
            { "Nullable", "enable" }
        };

        // Act
        var result = _generator.GenerateBuildPropsContent(commonProperties);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();

        // This will throw if the XML is invalid
        var xDoc = XDocument.Parse(result);
        xDoc.ShouldNotBeNull();
    }

    [Fact]
    public void GenerateBuildPropsContent_ShouldHandleEmptyCommonProperties()
    {
        // Arrange
        var commonProperties = new Dictionary<string, string>();

        // Act
        var result = _generator.GenerateBuildPropsContent(commonProperties);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();

        var xDoc = XDocument.Parse(result);
        var propertyGroup = xDoc.Root!.Element("PropertyGroup");

        propertyGroup.ShouldNotBeNull();

        // Default values for standard properties
        propertyGroup.Element("ImplicitUsings").ShouldNotBeNull();
        propertyGroup.Element("ImplicitUsings")!.Value.ShouldBe("enable");

        propertyGroup.Element("Nullable").ShouldNotBeNull();
        propertyGroup.Element("Nullable")!.Value.ShouldBe("enable");

        // No TargetFramework default
        propertyGroup.Element("TargetFramework").ShouldBeNull();
    }
}