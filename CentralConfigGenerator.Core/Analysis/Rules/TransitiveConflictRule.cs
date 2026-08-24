namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Reports packages whose transitive graph resolves to more than one version.</summary>
public sealed class TransitiveConflictRule : IAnalysisRule
{
    public string RuleId => RuleIds.TransitiveConflict;

    public string Title => "Divergent transitive resolution";

    public string Description =>
        "The same package resolves to different versions in different projects or frameworks.";

    public string Rationale =>
        "Divergence is invisible in the project files but very visible at runtime, where one "
        + "assembly loads and the other does not. Pinning centrally collapses the graph.";

    public Severity DefaultSeverity => Severity.Moderate;

    public bool IsEnabled(AnalysisOptions options) => options.Transitive;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        if (!context.Graph.IsAvailable)
        {
            yield break;
        }

        foreach (var group in context.Graph.DivergentPackages)
        {
            if (context.IsIgnored(group.Key))
            {
                continue;
            }

            var versions = group
                .Select(p => p.ResolvedVersion)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var highest = versions[^1];
            var isDirect = group.Any(p => !p.IsTransitive);

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = isDirect ? Severity.Moderate : Severity.Low,
                PackageId = group.Key,
                Version = highest,
                Message =
                    $"'{group.Key}' resolves to {versions.Count} versions across the graph: {string.Join(", ", versions)}.",
                Recommendation =
                    $"Pin '{group.Key}' to {highest} centrally"
                    + (context.TransitivePinningEnabled
                        ? "."
                        : " and enable CentralPackageTransitivePinningEnabled."),
                Fix = FixKind.SetCentralVersion,
                FixValue = highest,
            };
        }
    }
}
