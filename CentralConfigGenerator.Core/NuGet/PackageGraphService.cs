using System.Text.Json;
using CentralConfigGenerator.Core.Services;

namespace CentralConfigGenerator.Core.NuGet;

public interface IPackageGraphService
{
    Task<PackageGraph> LoadAsync(
        string target,
        string workingDirectory,
        bool includeTransitive,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Builds the resolved package graph by shelling out to <c>dotnet list package --format json</c>,
/// which is the only source of truth for transitive resolution.
/// </summary>
public sealed class PackageGraphService(IDotNetCliService cli) : IPackageGraphService
{
    public async Task<PackageGraph> LoadAsync(
        string target,
        string workingDirectory,
        bool includeTransitive,
        CancellationToken cancellationToken = default
    )
    {
        if (!cli.IsAvailable)
        {
            return PackageGraph.Empty with { Error = "The dotnet CLI was not found on PATH." };
        }

        var arguments = $"list \"{target}\" package --format json";
        if (includeTransitive)
        {
            arguments += " --include-transitive";
        }

        var result = await cli.RunAsync(
            arguments,
            workingDirectory,
            TimeSpan.FromMinutes(5),
            cancellationToken
        );

        if (!result.Succeeded)
        {
            return PackageGraph.Empty with
            {
                Error = Summarize(result.CombinedOutput),
            };
        }

        return Parse(result.StandardOutput);
    }

    internal static PackageGraph Parse(string json)
    {
        var start = json.IndexOf('{');
        if (start < 0)
        {
            return PackageGraph.Empty with { Error = "dotnet list package returned no JSON." };
        }

        var packages = new List<ResolvedPackage>();

        try
        {
            using var document = JsonDocument.Parse(json[start..]);

            if (!document.RootElement.TryGetProperty("projects", out var projects))
            {
                return PackageGraph.Empty with { Error = "Unexpected dotnet list package output." };
            }

            foreach (var project in projects.EnumerateArray())
            {
                var projectPath = project.TryGetProperty("path", out var path)
                    ? path.GetString() ?? string.Empty
                    : string.Empty;

                if (!project.TryGetProperty("frameworks", out var frameworks))
                {
                    continue;
                }

                foreach (var framework in frameworks.EnumerateArray())
                {
                    var tfm = framework.TryGetProperty("framework", out var name)
                        ? name.GetString() ?? string.Empty
                        : string.Empty;

                    AddPackages(framework, "topLevelPackages", false);
                    AddPackages(framework, "transitivePackages", true);

                    void AddPackages(JsonElement source, string property, bool transitive)
                    {
                        if (!source.TryGetProperty(property, out var list))
                        {
                            return;
                        }

                        foreach (var entry in list.EnumerateArray())
                        {
                            var id = entry.TryGetProperty("id", out var idValue)
                                ? idValue.GetString()
                                : null;

                            var resolved = entry.TryGetProperty("resolvedVersion", out var rv)
                                ? rv.GetString()
                                : null;

                            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(resolved))
                            {
                                continue;
                            }

                            packages.Add(
                                new ResolvedPackage
                                {
                                    PackageId = id,
                                    ResolvedVersion = resolved,
                                    RequestedVersion = entry.TryGetProperty(
                                        "requestedVersion",
                                        out var requested
                                    )
                                        ? requested.GetString()
                                        : null,
                                    ProjectPath = projectPath,
                                    TargetFramework = tfm,
                                    IsTransitive = transitive,
                                }
                            );
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            return PackageGraph.Empty with { Error = ex.Message };
        }

        return new PackageGraph { Packages = packages, IsAvailable = true };
    }

    private static string Summarize(string output)
    {
        var lines = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var error = lines.FirstOrDefault(l =>
            l.Contains("error", StringComparison.OrdinalIgnoreCase)
        );

        return error ?? (lines.Count > 0 ? lines[^1] : "dotnet list package failed.");
    }
}
