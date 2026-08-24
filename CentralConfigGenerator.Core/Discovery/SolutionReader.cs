using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CentralConfigGenerator.Core.Discovery;

/// <summary>
/// Reads project paths out of <c>.sln</c>, <c>.slnx</c> and <c>.slnf</c> files.
/// </summary>
public static partial class SolutionReader
{
    public static bool IsSolution(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".sln" or ".slnx" or ".slnf";

    public static IReadOnlyList<string> ReadProjects(string solutionPath, string content)
    {
        return Path.GetExtension(solutionPath).ToLowerInvariant() switch
        {
            ".slnx" => ReadSlnx(solutionPath, content),
            ".slnf" => ReadSlnf(solutionPath, content),
            _ => ReadSln(solutionPath, content),
        };
    }

    private static IReadOnlyList<string> ReadSln(string solutionPath, string content)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(solutionPath)) ?? ".";
        var results = new List<string>();

        foreach (Match match in ProjectLineRegex().Matches(content))
        {
            var relative = match.Groups["path"].Value.Trim();
            if (relative.Length == 0 || !HasProjectExtension(relative))
            {
                continue;
            }

            results.Add(Normalize(root, relative));
        }

        return results;
    }

    private static IReadOnlyList<string> ReadSlnx(string solutionPath, string content)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(solutionPath)) ?? ".";
        var results = new List<string>();

        XDocument document;
        try
        {
            document = XDocument.Parse(content);
        }
        catch (System.Xml.XmlException)
        {
            return results;
        }

        foreach (var element in document.Descendants("Project"))
        {
            var relative = element.Attribute("Path")?.Value;
            if (string.IsNullOrWhiteSpace(relative) || !HasProjectExtension(relative))
            {
                continue;
            }

            results.Add(Normalize(root, relative));
        }

        return results;
    }

    private static IReadOnlyList<string> ReadSlnf(string solutionPath, string content)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(solutionPath)) ?? ".";
        var results = new List<string>();

        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("solution", out var solution))
            {
                return results;
            }

            if (solution.TryGetProperty("path", out var slnPath) && slnPath.GetString() is { } sln)
            {
                root = Path.GetDirectoryName(Path.GetFullPath(Normalize(root, sln))) ?? root;
            }

            if (!solution.TryGetProperty("projects", out var projects))
            {
                return results;
            }

            foreach (var project in projects.EnumerateArray())
            {
                if (project.GetString() is { } relative && HasProjectExtension(relative))
                {
                    results.Add(Normalize(root, relative));
                }
            }
        }
        catch (JsonException)
        {
            // Malformed filter files simply yield no projects.
        }

        return results;
    }

    private static bool HasProjectExtension(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".csproj" or ".fsproj" or ".vbproj";

    private static string Normalize(string root, string relative)
    {
        var normalized = relative.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        return Path.GetFullPath(
            Path.IsPathRooted(normalized) ? normalized : Path.Combine(root, normalized)
        );
    }

    [GeneratedRegex(
        "^Project\\(\"\\{[^}]+\\}\"\\)\\s*=\\s*\"[^\"]*\"\\s*,\\s*\"(?<path>[^\"]+)\"",
        RegexOptions.Multiline | RegexOptions.CultureInvariant
    )]
    private static partial Regex ProjectLineRegex();
}
