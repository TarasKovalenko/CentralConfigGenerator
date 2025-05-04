using System.Xml.Linq;
using CentralConfigGenerator.Core.Generators.Abstractions;

namespace CentralConfigGenerator.Core.Generators;

public class BuildPropsGenerator : IBuildPropsGenerator
{
    public string GenerateBuildPropsContent(Dictionary<string, string> commonProperties)
    {
        var xDoc = new XDocument(new XElement("Project", new XElement("PropertyGroup")));

        var propertyGroup = xDoc.Root!.Element("PropertyGroup")!;

        // Define the required properties we want to include

        var requiredPropertyNames = new[] { "TargetFramework", "ImplicitUsings", "Nullable" };

        // Add the properties that match our required list
        foreach (var propertyName in requiredPropertyNames)
        {
            if (commonProperties.TryGetValue(propertyName, out var propertyValue))
            {
                propertyGroup.Add(new XElement(propertyName, propertyValue));
            }
            else
            {
                // Add default values for required properties that weren't found in the projects
                switch (propertyName)
                {
                    case "ImplicitUsings":
                    case "Nullable":
                        propertyGroup.Add(new XElement(propertyName, "enable"));
                        break;
                }
            }
        }

        return xDoc.ToString();
    }
}
