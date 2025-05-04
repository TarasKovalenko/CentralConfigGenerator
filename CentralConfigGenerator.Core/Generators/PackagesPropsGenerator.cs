using System.Xml.Linq;
using CentralConfigGenerator.Core.Generators.Abstractions;

namespace CentralConfigGenerator.Core.Generators;

public class PackagesPropsGenerator : IPackagesPropsGenerator
{
    public string GeneratePackagesPropsContent(Dictionary<string, string> packageVersions)
    {
        var xDoc = new XDocument(
            new XElement(
                "Project",
                new XElement(
                    "PropertyGroup",
                    new XElement("ManagePackageVersionsCentrally", "true")
                ),
                new XElement("ItemGroup")
            )
        );

        var itemGroup = xDoc.Root!.Element("ItemGroup")!;

        foreach (var package in packageVersions.OrderBy(p => p.Key))
        {
            itemGroup.Add(
                new XElement(
                    "PackageVersion",
                    new XAttribute("Include", package.Key),
                    new XAttribute("Version", package.Value)
                )
            );
        }

        return xDoc.ToString();
    }
}
