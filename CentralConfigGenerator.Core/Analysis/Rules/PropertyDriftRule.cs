using CentralConfigGenerator.Core.Migration;

namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Reports properties repeated identically in every project, which belong in Directory.Build.props.</summary>
public sealed class PropertyDriftRule : IAnalysisRule
{
    public string RuleId => RuleIds.PropertyDrift;

    public string Title => "Property duplicated across projects";

    public string Description =>
        "Several projects declare the same MSBuild property with the same value.";

    public string Rationale =>
        "Copies drift. One project eventually gets edited and the difference is invisible in "
        + "review. Directory.Build.props makes the value single-sourced.";

    public Severity DefaultSeverity => Severity.Info;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        if (context.Projects.Count < 2)
        {
            yield break;
        }

        var hoistable = PropertyHoister.FindHoistableProperties(
            context.Projects,
            BuildPropertyDefaults.Recommended
        );

        foreach (var (name, value) in hoistable.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            yield return new Finding
            {
                RuleId = RuleId,
                Severity = DefaultSeverity,
                Subject = name,
                Message = $"'{name}' is set to '{value}' in every project that declares it.",
                // Hoisting also has to delete the property from every project, which is what the
                // 'build' command does transactionally. Doing half of it here would be worse.
                Recommendation =
                    $"Move {name} into Directory.Build.props (run 'central-config build' or 'migrate --unify-props').",
            };
        }
    }
}
