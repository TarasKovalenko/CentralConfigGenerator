using System.Xml.Linq;

namespace CentralConfigGenerator.Core.Generators;

public class BuildPropsGenerator : IBuildPropsGenerator
{
    public string GenerateBuildPropsContent(Dictionary<string, string> commonProperties)
    {
        var xDoc = new XDocument(
            new XElement("Project",
                new XElement("PropertyGroup")
            )
        );

        var propertyGroup = xDoc.Root!.Element("PropertyGroup")!;

        // Common .NET properties to include by default if not overridden
        var defaultProperties = new Dictionary<string, string>
        {
            { "LangVersion", "latest" },
            { "Nullable", "enable" },
            { "ImplicitUsings", "enable" }
        };

        // First add common properties found in projects
        foreach (var property in commonProperties.OrderBy(p => p.Key))
        {
            propertyGroup.Add(new XElement(property.Key, property.Value));

            // Remove from defaults if already covered
            if (defaultProperties.ContainsKey(property.Key))
            {
                defaultProperties.Remove(property.Key);
            }
        }

        // Now add any remaining default properties
        foreach (var property in defaultProperties)
        {
            propertyGroup.Add(new XElement(property.Key, property.Value));
        }

        // Format the XML nicely with proper indentation
        return xDoc.ToString();
    }
}

public interface IBuildPropsGenerator
{
    string GenerateBuildPropsContent(Dictionary<string, string> commonProperties);
}