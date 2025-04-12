using System.Xml.Linq;

namespace CentralConfigGenerator.Core.Generators;

public class PackagesPropsGenerator : IPackagesPropsGenerator
{
    public string GeneratePackagesPropsContent(Dictionary<string, string> packageVersions)
    {
        var xDoc = new XDocument(
            new XElement("Project",
                new XElement("PropertyGroup",
                    new XElement("ManagePackageVersionsCentrally", "true")
                ),
                new XElement("ItemGroup")
            )
        );

        var itemGroup = xDoc.Root!.Element("ItemGroup")!;

        // Add packages in alphabetical order
        foreach (var package in packageVersions.OrderBy(p => p.Key))
        {
            itemGroup.Add(
                new XElement("PackageVersion",
                    new XAttribute("Include", package.Key),
                    new XAttribute("Version", package.Value)
                )
            );
        }

        // Format the XML nicely with proper indentation
        return xDoc.ToString();
    }
}

public interface IPackagesPropsGenerator
{
    string GeneratePackagesPropsContent(Dictionary<string, string> packageVersions);
}