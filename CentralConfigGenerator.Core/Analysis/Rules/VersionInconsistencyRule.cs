using CentralConfigGenerator.Core.Services;

namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Flags packages referenced with more than one version across the workspace.</summary>
public sealed class VersionInconsistencyRule : IAnalysisRule
{
    public string RuleId => RuleIds.VersionInconsistency;

    public string Title => "Inconsistent package versions";

    public string Description =>
        "The same package is referenced with different versions by different projects.";

    public string Rationale =>
        "Divergent versions produce different binaries per project and make runtime binding "
        + "failures likely. Central package management removes the drift by pinning one version.";

    public Severity DefaultSeverity => Severity.Moderate;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var groups = context
            .RelevantReferences.Where(r => !string.IsNullOrWhiteSpace(r.Version))
            .GroupBy(r => r.PackageId, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var versions = group
                .Select(r => r.Version!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (versions.Count < 2)
            {
                continue;
            }

            var highest = versions
                .OrderBy(v => v, Comparer<string>.Create((a, b) =>
                    VersionSelector.Compare(a, b, global::NuGet.Versioning.VersionComparison.VersionRelease)))
                .Last();

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = DefaultSeverity,
                PackageId = group.Key,
                Message =
                    $"'{group.Key}' is referenced with {versions.Count} versions: {string.Join(", ", versions)}.",
                Recommendation =
                    $"Pin a single version (highest observed is {highest}) in Directory.Packages.props.",
                Fix = FixKind.SetCentralVersion,
                FixValue = highest,
                Version = highest,
            };
        }
    }
}
