using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Xml;

namespace CentralConfigGenerator.Core.Migration;

/// <summary>
/// Works out which MSBuild properties every project agrees on, so they can be lifted into
/// Directory.Build.props without changing the evaluated build.
/// </summary>
public static class PropertyHoister
{
    /// <summary>
    /// Returns the properties whose value is identical across <em>all</em> projects that declare
    /// them, and that are declared by at least two projects. Requiring unanimity is what keeps the
    /// migration behaviour-preserving.
    /// </summary>
    public static Dictionary<string, string> FindHoistableProperties(
        IReadOnlyCollection<ProjectFile> projects,
        IReadOnlyList<string> candidateNames,
        int minimumProjects = 2
    )
    {
        var allowed = new HashSet<string>(candidateNames, StringComparer.OrdinalIgnoreCase);
        var blocked = new HashSet<string>(
            BuildPropertyDefaults.NeverHoist,
            StringComparer.OrdinalIgnoreCase
        );

        var observed = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var declaringProjects = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects)
        {
            if (!MsBuildXml.TryParse(project.Content, out var document, out _))
            {
                continue;
            }

            var seenInThisProject = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in MsBuildXml.Descendants(document, "PropertyGroup"))
            {
                // Conditioned groups vary per configuration and are never safe to hoist.
                if (!string.IsNullOrWhiteSpace(MsBuildXml.AttributeValue(group, "Condition")))
                {
                    continue;
                }

                foreach (var element in group.Elements())
                {
                    var name = element.Name.LocalName;

                    if (blocked.Contains(name) || (allowed.Count > 0 && !allowed.Contains(name)))
                    {
                        continue;
                    }

                    var value = element.Value.Trim();
                    if (value.Length == 0)
                    {
                        continue;
                    }

                    if (!observed.TryGetValue(name, out var values))
                    {
                        values = new HashSet<string>(StringComparer.Ordinal);
                        observed[name] = values;
                    }

                    values.Add(value);
                    seenInThisProject.Add(name);
                }
            }

            foreach (var name in seenInThisProject)
            {
                declaringProjects[name] = declaringProjects.GetValueOrDefault(name) + 1;
            }
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, values) in observed)
        {
            if (values.Count != 1)
            {
                continue;
            }

            if (declaringProjects.GetValueOrDefault(name) < minimumProjects)
            {
                continue;
            }

            result[name] = values.First();
        }

        return result;
    }
}
