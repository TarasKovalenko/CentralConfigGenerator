using CentralConfigGenerator.Core.IO;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Discovery;

/// <summary>
/// Finds project files by scanning a directory tree or by walking a solution, preserving each
/// file's original encoding and line endings so they can be rewritten faithfully.
/// </summary>
public sealed class ProjectDiscoveryService(IFileSystem fileSystem) : IProjectDiscoveryService
{
    public async Task<IReadOnlyList<ProjectFile>> DiscoverAsync(
        DiscoveryOptions options,
        CancellationToken cancellationToken = default
    )
    {
        var paths = ResolvePaths(options);
        var results = new List<ProjectFile>(paths.Count);

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = await LoadAsync(path, cancellationToken);
            if (file is not null)
            {
                results.Add(file);
            }
        }

        return results
            .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ProjectFile?> LoadAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        if (!fileSystem.FileExists(path))
        {
            return null;
        }

        var bytes = await fileSystem.ReadAllBytesAsync(path, cancellationToken);
        var (content, format) = EncodingDetector.Decode(bytes);

        return new ProjectFile
        {
            Path = path,
            Content = content,
            Kind = ProjectKindExtensions.FromPath(path),
            Format = format,
        };
    }

    public async Task SaveAsync(
        ProjectFile projectFile,
        CancellationToken cancellationToken = default
    )
    {
        var bytes = EncodingDetector.Encode(projectFile.Content, projectFile.Format);
        await fileSystem.WriteAllBytesAsync(projectFile.Path, bytes, cancellationToken);
    }

    private IReadOnlyList<string> ResolvePaths(DiscoveryOptions options)
    {
        var kinds = options.IncludedKinds.ToHashSet();

        if (!string.IsNullOrWhiteSpace(options.ProjectPath))
        {
            var project = Path.GetFullPath(options.ProjectPath);
            if (fileSystem.FileExists(project))
            {
                return [project];
            }

            if (fileSystem.DirectoryExists(project))
            {
                return ScanDirectory(project, options, kinds);
            }

            return [];
        }

        if (!string.IsNullOrWhiteSpace(options.SolutionPath))
        {
            var solution = Path.GetFullPath(options.SolutionPath);
            if (fileSystem.FileExists(solution) && SolutionReader.IsSolution(solution))
            {
                var content = fileSystem.ReadAllTextAsync(solution).GetAwaiter().GetResult();
                return SolutionReader
                    .ReadProjects(solution, content)
                    .Where(fileSystem.FileExists)
                    .Where(p => kinds.Contains(ProjectKindExtensions.FromPath(p)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (fileSystem.DirectoryExists(solution))
            {
                return ScanDirectory(solution, options, kinds);
            }

            return [];
        }

        return ScanDirectory(Path.GetFullPath(options.RootDirectory), options, kinds);
    }

    private IReadOnlyList<string> ScanDirectory(
        string root,
        DiscoveryOptions options,
        HashSet<ProjectKind> kinds
    )
    {
        if (!fileSystem.DirectoryExists(root))
        {
            return [];
        }

        var excludeRegex = options.CompileExcludeRegex();
        var results = new List<string>();

        foreach (var path in fileSystem.EnumerateFiles(root, "*", options.Recursive))
        {
            var kind = ProjectKindExtensions.FromPath(path);
            if (!kinds.Contains(kind))
            {
                continue;
            }

            if (IsExcluded(root, path, options, excludeRegex))
            {
                continue;
            }

            results.Add(path);
        }

        return results;
    }

    internal static bool IsExcluded(
        string root,
        string path,
        DiscoveryOptions options,
        System.Text.RegularExpressions.Regex? excludeRegex
    )
    {
        var relative = Path.GetRelativePath(root, path);
        var segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries
        );

        if (options.ApplyDefaultExclusions)
        {
            // The final segment is the file name itself and must not be matched against directory names.
            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (
                    DiscoveryOptions.DefaultExcludedDirectories.Contains(
                        segments[i],
                        StringComparer.OrdinalIgnoreCase
                    )
                )
                {
                    return true;
                }
            }
        }

        if (excludeRegex is null)
        {
            return false;
        }

        return excludeRegex.IsMatch(relative.Replace(Path.DirectorySeparatorChar, '/'))
            || segments.Any(excludeRegex.IsMatch);
    }
}
