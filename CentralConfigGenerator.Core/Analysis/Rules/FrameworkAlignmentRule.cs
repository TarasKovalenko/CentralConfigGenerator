namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Reports target-framework drift across the workspace.</summary>
public sealed class FrameworkAlignmentRule : IAnalysisRule
{
    public string RuleId => RuleIds.FrameworkAlignment;

    public string Title => "Target framework drift";

    public string Description =>
        "Projects in the same workspace target different framework versions.";

    public string Rationale =>
        "Mixed target frameworks split the dependency graph: each TFM resolves packages "
        + "independently, so a single central version can still produce two different builds.";

    public Severity DefaultSeverity => Severity.Low;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        if (context.TargetFrameworks.Count == 0)
        {
            yield break;
        }

        var distinct = context
            .TargetFrameworks.SelectMany(pair => pair.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count < 2)
        {
            yield break;
        }

        // The dominant framework is the one the most projects can build against.
        var dominant = distinct
            .OrderByDescending(tfm =>
                context.TargetFrameworks.Count(pair =>
                    pair.Value.Contains(tfm, StringComparer.OrdinalIgnoreCase)
                )
            )
            .ThenByDescending(tfm => tfm, StringComparer.OrdinalIgnoreCase)
            .First();

        foreach (var (projectPath, frameworks) in context.TargetFrameworks)
        {
            // A multi-targeted project that includes the dominant framework is aligned already.
            if (frameworks.Contains(dominant, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = DefaultSeverity,
                ProjectPath = projectPath,
                Subject = string.Join(";", frameworks),
                Message =
                    $"{Path.GetFileName(projectPath)} targets {string.Join(", ", frameworks)} "
                    + $"but not {dominant}, which the rest of the workspace builds against.",
                Recommendation =
                    $"Add {dominant} to its target frameworks, or hoist TargetFramework into Directory.Build.props deliberately.",
            };
        }
    }
}
