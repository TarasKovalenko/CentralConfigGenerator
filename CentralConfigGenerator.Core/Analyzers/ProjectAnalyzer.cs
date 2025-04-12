using System.Xml.Linq;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Analyzers;

public class ProjectAnalyzer : IProjectAnalyzer
{
    public Dictionary<string, string> ExtractCommonProperties(IReadOnlyCollection<ProjectFile> projectFiles)
    {
        var propertyCountsByName = new Dictionary<string, Dictionary<string, int>>();

        foreach (var projectFile in projectFiles)
        {
            try
            {
                var xDoc = XDocument.Parse(projectFile.Content);
                var propertyGroups = xDoc.Descendants("PropertyGroup");

                foreach (var propertyGroup in propertyGroups)
                {
                    foreach (var element in propertyGroup.Elements())
                    {
                        var propertyName = element.Name.LocalName;
                        var propertyValue = element.Value;

                        // Skip empty properties
                        if (string.IsNullOrWhiteSpace(propertyValue))
                        {
                            continue;
                        }

                        // Track property names and values
                        if (!propertyCountsByName.ContainsKey(propertyName))
                        {
                            propertyCountsByName[propertyName] = new Dictionary<string, int>();
                        }

                        // Track frequency of each value for this property
                        if (!propertyCountsByName[propertyName].ContainsKey(propertyValue))
                        {
                            propertyCountsByName[propertyName][propertyValue] = 0;
                        }

                        propertyCountsByName[propertyName][propertyValue]++;
                    }
                }
            }
            catch (Exception)
            {
                // todo: Log error but continue with other projects
                continue;
            }
        }

        // Define the threshold - properties must appear in at least half of the projects
        // with the same value to be considered "common"
        var threshold = Math.Max(1, projectFiles.Count / 2);

        // Find properties that meet the threshold
        var commonProperties = new Dictionary<string, string>();

        foreach (var property in propertyCountsByName)
        {
            var propertyName = property.Key;
            var mostCommonValue = property.Value.OrderByDescending(v => v.Value).FirstOrDefault();

            // Only include if this property value appears in enough projects
            if (mostCommonValue.Value >= threshold)
            {
                commonProperties[propertyName] = mostCommonValue.Key;
            }
        }

        return commonProperties;
    }
}

public interface IProjectAnalyzer
{
    Dictionary<string, string> ExtractCommonProperties(IReadOnlyCollection<ProjectFile> projectFiles);
}