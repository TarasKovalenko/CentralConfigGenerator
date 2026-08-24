using System.Text;
using CentralConfigGenerator.Core.NuGet;

namespace CentralConfigGenerator.Core.Workspace;

/// <summary>Renders the resolved dependency graph as an ASCII tree.</summary>
public sealed class DependencyTreeService(IPackageGraphService graphService)
{
    public async Task<string> RenderAsync(
        string target,
        string workingDirectory,
        bool includeTransitive,
        CancellationToken cancellationToken = default
    )
    {
        var graph = await graphService.LoadAsync(
            target,
            workingDirectory,
            includeTransitive,
            cancellationToken
        );

        if (!graph.IsAvailable)
        {
            return $"Dependency graph unavailable: {graph.Error}";
        }

        return Render(graph, includeTransitive);
    }

    internal static string Render(PackageGraph graph, bool includeTransitive)
    {
        var builder = new StringBuilder();

        var byProject = graph
            .Packages.GroupBy(p => p.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var projectIndex = 0; projectIndex < byProject.Count; projectIndex++)
        {
            var project = byProject[projectIndex];
            builder.Append(Path.GetFileNameWithoutExtension(project.Key)).Append('\n');

            var frameworks = project
                .GroupBy(p => p.TargetFramework, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var frameworkIndex = 0; frameworkIndex < frameworks.Count; frameworkIndex++)
            {
                var framework = frameworks[frameworkIndex];
                var isLastFramework = frameworkIndex == frameworks.Count - 1;
                var frameworkPrefix = isLastFramework ? "└── " : "├── ";
                var childIndent = isLastFramework ? "    " : "│   ";

                builder.Append(frameworkPrefix).Append(framework.Key).Append('\n');

                var direct = framework
                    .Where(p => !p.IsTransitive)
                    .OrderBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var transitive = includeTransitive
                    ? framework
                        .Where(p => p.IsTransitive)
                        .OrderBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                    : [];

                var total = direct.Count + transitive.Count;
                var written = 0;

                foreach (var package in direct)
                {
                    written++;
                    var last = written == total;
                    builder
                        .Append(childIndent)
                        .Append(last ? "└── " : "├── ")
                        .Append(package.PackageId)
                        .Append(' ')
                        .Append(package.ResolvedVersion);

                    if (
                        package.RequestedVersion is { } requested
                        && !string.Equals(requested, package.ResolvedVersion, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        builder.Append(" (requested ").Append(requested).Append(')');
                    }

                    builder.Append('\n');
                }

                foreach (var package in transitive)
                {
                    written++;
                    var last = written == total;
                    builder
                        .Append(childIndent)
                        .Append(last ? "└── " : "├── ")
                        .Append(package.PackageId)
                        .Append(' ')
                        .Append(package.ResolvedVersion)
                        .Append(" (transitive)\n");
                }
            }

            if (projectIndex < byProject.Count - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }
}
