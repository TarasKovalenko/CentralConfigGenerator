using System.Xml.Linq;
using CentralConfigGenerator.Core.Analyzers.Abstractions;
using CentralConfigGenerator.Core.Models;
using Spectre.Console;

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
            catch (Exception ex)
            {
                var errorMessage = $"Error extracting properties from project file: {projectFile.Path}";
                AnsiConsole.MarkupLineInterpolated($"[red]Error:{errorMessage}[/]");
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            }
        }

        // Define the threshold - for something to be common, it needs to appear in all projects,
        // or at least in 2 projects for the case of only 2 projects
        int threshold = projectFiles.Count switch
        {
            <= 1 => projectFiles.Count,
            2 => 2,
            _ => (int)Math.Ceiling(projectFiles.Count / 2.0)
        };

        // Find properties that meet the threshold
        var commonProperties = new Dictionary<string, string>();

        foreach (var property in propertyCountsByName)
        {
            var propertyName = property.Key;
            var mostCommonValue = property.Value.OrderByDescending(v => v.Value).FirstOrDefault();

            // For the property to be common, it should appear in at least 'threshold' number of projects
            // with the same value
            if (mostCommonValue.Value >= threshold)
            {
                commonProperties[propertyName] = mostCommonValue.Key;
            }
        }

        return commonProperties;
    }
}