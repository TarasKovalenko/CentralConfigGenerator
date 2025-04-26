using CentralConfigGenerator.Core.Generators;
using System.Xml.Linq;

namespace CentralConfigGenerator.Core.Tests.Generators;

public class PackagesPropsGeneratorTests
{
    private readonly IPackagesPropsGenerator _generator = new PackagesPropsGenerator();

    [Fact]
    public void GeneratePackagesPropsContent_ShouldCreateValidXml()
    {
        // Arrange
        var packageVersions = new Dictionary<string, string>
        {
            { "Package1", "1.0.0" },
            { "Package2", "2.0.0" }
        };

        // Act
        var result = _generator.GeneratePackagesPropsContent(packageVersions);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
        
        // Should be valid XML
        var xDoc = XDocument.Parse(result);
        xDoc.ShouldNotBeNull();
        
        // Should have Project root
        xDoc.Root!.Name.LocalName.ShouldBe("Project");
        
        // Should have PropertyGroup with ManagePackageVersionsCentrally set to true
        var propertyGroup = xDoc.Root.Element("PropertyGroup");
        propertyGroup.ShouldNotBeNull();
        var managePackagesElement = propertyGroup.Element("ManagePackageVersionsCentrally");
        managePackagesElement.ShouldNotBeNull();
        managePackagesElement.Value.ShouldBe("true");
        
        // Should have ItemGroup with PackageVersion elements
        var itemGroup = xDoc.Root.Element("ItemGroup");
        itemGroup.ShouldNotBeNull();
        var packageVersionElements = itemGroup.Elements("PackageVersion").ToList();
        packageVersionElements.Count.ShouldBe(2);
        
        // PackageVersion elements should have correct attributes
        var package1Element = packageVersionElements.FirstOrDefault(e => e.Attribute("Include")?.Value == "Package1");
        package1Element.ShouldNotBeNull();
        package1Element.Attribute("Version")!.Value.ShouldBe("1.0.0");
        
        var package2Element = packageVersionElements.FirstOrDefault(e => e.Attribute("Include")?.Value == "Package2");
        package2Element.ShouldNotBeNull();
        package2Element.Attribute("Version")!.Value.ShouldBe("2.0.0");
    }

    [Fact]
    public void GeneratePackagesPropsContent_ShouldHandleEmptyDictionary()
    {
        // Arrange
        var packageVersions = new Dictionary<string, string>();

        // Act
        var result = _generator.GeneratePackagesPropsContent(packageVersions);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
        
        var xDoc = XDocument.Parse(result);
        xDoc.ShouldNotBeNull();
        
        var itemGroup = xDoc.Root!.Element("ItemGroup");
        itemGroup.ShouldNotBeNull();
        
        // Should have no PackageVersion elements
        var packageVersionElements = itemGroup.Elements("PackageVersion").ToList();
        packageVersionElements.Count.ShouldBe(0);
    }

    [Fact]
    public void GeneratePackagesPropsContent_ShouldOrderPackagesAlphabetically()
    {
        // Arrange
        var packageVersions = new Dictionary<string, string>
        {
            { "ZPackage", "3.0.0" },
            { "APackage", "1.0.0" },
            { "MPackage", "2.0.0" }
        };

        // Act
        var result = _generator.GeneratePackagesPropsContent(packageVersions);

        // Assert
        var xDoc = XDocument.Parse(result);
        var packageVersionElements = xDoc.Root!.Element("ItemGroup")!.Elements("PackageVersion").ToList();
        
        packageVersionElements.Count.ShouldBe(3);
        
        // Should be ordered alphabetically
        packageVersionElements[0].Attribute("Include")!.Value.ShouldBe("APackage");
        packageVersionElements[1].Attribute("Include")!.Value.ShouldBe("MPackage");
        packageVersionElements[2].Attribute("Include")!.Value.ShouldBe("ZPackage");
    }

    [Fact]
    public void GeneratePackagesPropsContent_ShouldHandleSpecialVersionFormats()
    {
        // Arrange
        var packageVersions = new Dictionary<string, string>
        {
            { "Package1", "1.0.0-preview.1" },
            { "Package2", "2.0.0-beta+metadata" },
            { "Package3", "$(VariableVersion)" }
        };

        // Act
        var result = _generator.GeneratePackagesPropsContent(packageVersions);

        // Assert
        var xDoc = XDocument.Parse(result);
        var packageVersionElements = xDoc.Root!.Element("ItemGroup")!.Elements("PackageVersion").ToList();
        
        packageVersionElements.Count.ShouldBe(3);
        
        // Should preserve special version formats
        var versions = packageVersionElements.Select(e => e.Attribute("Version")!.Value).ToList();
        versions.ShouldContain("1.0.0-preview.1");
        versions.ShouldContain("2.0.0-beta+metadata");
        versions.ShouldContain("$(VariableVersion)");
    }
}