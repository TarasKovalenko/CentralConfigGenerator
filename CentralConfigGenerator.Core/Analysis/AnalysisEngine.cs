using System.Diagnostics;
using System.Xml.Linq;
using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.IO;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.NuGet;
using CentralConfigGenerator.Core.Xml;

namespace CentralConfigGenerator.Core.Analysis;

public interface IAnalysisEngine
{
    Task<AnalysisReport> AnalyzeAsync(
        AnalysisOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    );

    Task<AnalysisContext> BuildContextAsync(
        AnalysisOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Builds the analysis context once, runs every enabled rule against it, then applies severity
/// overrides and the baseline.
/// </summary>
public sealed class AnalysisEngine(
    IProjectDiscoveryService discovery,
    IFileSystem fileSystem,
    INuGetFeedService feedService,
    IPackageGraphService graphService,
    IBaselineService baselineService
) : IAnalysisEngine
{
    public async Task<AnalysisReport> AnalyzeAsync(
        AnalysisOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var context = await BuildContextAsync(options, progress, cancellationToken);

        if (context.Projects.Count == 0)
        {
            return new AnalysisReport
            {
                Findings = [],
                ProjectCount = 0,
                PackageCount = 0,
                Duration = stopwatch.Elapsed,
                RootDirectory = options.RootDirectory,
            };
        }

        var findings = new List<Finding>();
        var incompletions = new List<string>();

        foreach (var rule in RuleCatalog.CreateAll())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsRuleActive(rule, options, context))
            {
                continue;
            }

            progress?.Report(rule.RuleId);

            try
            {
                foreach (var finding in rule.Analyze(context))
                {
                    findings.Add(ApplySeverityOverride(finding, options));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                incompletions.Add($"{rule.RuleId} failed: {ex.Message}");
            }
        }

        if (options.RequiresNetwork)
        {
            var unreachable = context
                .Metadata.Values.Where(m => m.Error is not null)
                .Select(m => $"{m.PackageId}: {m.Error}")
                .ToList();

            if (unreachable.Count > 0)
            {
                incompletions.Add(
                    $"{unreachable.Count} package(s) could not be checked against the feed."
                );
            }
        }

        if (options.Transitive && !context.Graph.IsAvailable)
        {
            incompletions.Add(
                $"Transitive graph unavailable: {context.Graph.Error ?? "unknown reason"}."
            );
        }

        var baseline = options.BaselinePath is null
            ? null
            : await baselineService.LoadAsync(options.BaselinePath, cancellationToken);

        var filtered = baselineService.Apply(findings, baseline);

        var ordered = filtered
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ThenBy(f => f.PackageId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AnalysisReport
        {
            Findings = ordered,
            ProjectCount = context.Projects.Count,
            PackageCount = context
                .References.Select(r => r.PackageId)
                .Concat(context.CentralVersions.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            Duration = stopwatch.Elapsed,
            IsIncomplete = incompletions.Count > 0,
            Incompletions = incompletions,
            RootDirectory = options.RootDirectory,
            SuppressedCount = findings.Count - filtered.Count,
        };
    }

    public async Task<AnalysisContext> BuildContextAsync(
        AnalysisOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        progress?.Report("Discovering projects");

        var projects = await discovery.DiscoverAsync(
            new DiscoveryOptions
            {
                RootDirectory = options.RootDirectory,
                SolutionPath = options.SolutionPath,
                ProjectPath = options.ProjectPath,
                ExcludePattern = options.ExcludePattern,
            },
            cancellationToken
        );

        var references = projects.SelectMany(PackageReferenceReader.Read).ToList();
        var frameworks = ReadTargetFrameworks(projects);

        var (centralVersions, cpmEnabled, pinning, propsPath) = await ReadCentralFileAsync(
            options,
            cancellationToken
        );

        var graph = PackageGraph.Empty;
        if (options.Transitive)
        {
            progress?.Report("Resolving transitive graph");
            graph = await graphService.LoadAsync(
                options.SolutionPath ?? options.ProjectPath ?? options.RootDirectory,
                options.RootDirectory,
                includeTransitive: true,
                cancellationToken
            );
        }

        IReadOnlyDictionary<string, PackageMetadataSnapshot> metadata =
            new Dictionary<string, PackageMetadataSnapshot>(StringComparer.OrdinalIgnoreCase);

        if (options.RequiresNetwork)
        {
            var ids = references
                .Select(r => r.PackageId)
                .Concat(centralVersions.Keys)
                .Concat(graph.Transitive.Select(p => p.PackageId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            progress?.Report($"Querying {ids.Count} packages");

            metadata = await feedService.GetMetadataAsync(
                ids,
                options.MaxParallelism,
                progress,
                cancellationToken
            );
        }

        return new AnalysisContext
        {
            Options = options,
            Projects = projects,
            References = references,
            CentralVersions = centralVersions,
            CentralPackageManagementEnabled = cpmEnabled,
            TransitivePinningEnabled = pinning,
            PackagesPropsPath = propsPath,
            Metadata = metadata,
            Graph = graph,
            TargetFrameworks = frameworks,
        };
    }

    private static bool IsRuleActive(
        IAnalysisRule rule,
        AnalysisOptions options,
        AnalysisContext context
    )
    {
        if (
            options.RuleOverrides.TryGetValue(rule.RuleId, out var severity)
            && severity is null
        )
        {
            return false;
        }

        if (!rule.IsEnabled(options))
        {
            return false;
        }

        if (rule.RequiresNetwork && !options.RequiresNetwork)
        {
            return false;
        }

        return !rule.RequiresCentralPackageManagement || context.CentralPackageManagementEnabled;
    }

    private static Finding ApplySeverityOverride(Finding finding, AnalysisOptions options) =>
        options.RuleOverrides.TryGetValue(finding.RuleId, out var severity) && severity is not null
            ? finding with { Severity = severity.Value }
            : finding;

    private async Task<(
        IReadOnlyDictionary<string, string> Versions,
        bool CpmEnabled,
        bool Pinning,
        string? Path
    )> ReadCentralFileAsync(AnalysisOptions options, CancellationToken cancellationToken)
    {
        var empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = FindPackagesProps(options);

        if (path is null)
        {
            return (empty, false, false, null);
        }

        var file = await discovery.LoadAsync(path, cancellationToken);
        if (file is null || !MsBuildXml.TryParse(file.Content, out var document, out _))
        {
            return (empty, false, false, path);
        }

        var versions = PackageReferenceReader.ReadPackageVersions(document);

        return (
            versions,
            ReadBool(document, "ManagePackageVersionsCentrally"),
            ReadBool(document, "CentralPackageTransitivePinningEnabled"),
            path
        );
    }

    private string? FindPackagesProps(AnalysisOptions options)
    {
        var start =
            options.SolutionPath is not null
                ? Path.GetDirectoryName(Path.GetFullPath(options.SolutionPath))
                : Path.GetFullPath(options.RootDirectory);

        var directory = start is null ? null : new DirectoryInfo(start);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Directory.Packages.props");
            if (fileSystem.FileExists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static bool ReadBool(XDocument document, string propertyName) =>
        MsBuildXml
            .Descendants(document, propertyName)
            .Select(e => e.Value.Trim())
            .Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadTargetFrameworks(
        IReadOnlyList<ProjectFile> projects
    )
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects)
        {
            if (!MsBuildXml.TryParse(project.Content, out var document, out _))
            {
                continue;
            }

            var frameworks = new List<string>();

            foreach (var element in MsBuildXml.Descendants(document, "TargetFramework"))
            {
                var value = element.Value.Trim();
                if (value.Length > 0 && !value.Contains('$'))
                {
                    frameworks.Add(value);
                }
            }

            foreach (var element in MsBuildXml.Descendants(document, "TargetFrameworks"))
            {
                foreach (
                    var value in element.Value.Split(
                        ';',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    )
                )
                {
                    if (!value.Contains('$'))
                    {
                        frameworks.Add(value);
                    }
                }
            }

            if (frameworks.Count > 0)
            {
                result[project.Path] = frameworks.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        return result;
    }
}
