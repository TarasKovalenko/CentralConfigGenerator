namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Reports packages the author has marked deprecated on the feed.</summary>
public sealed class DeprecatedPackageRule : IAnalysisRule
{
    public string RuleId => RuleIds.DeprecatedPackage;

    public string Title => "Deprecated package";

    public string Description => "The package author marked this package as deprecated.";

    public string Rationale =>
        "Deprecated packages stop receiving fixes, including security fixes. The feed usually "
        + "names a replacement, so the migration path is known.";

    public Severity DefaultSeverity => Severity.High;

    public bool RequiresNetwork => true;

    public bool IsEnabled(AnalysisOptions options) => options.Deprecated;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        foreach (var (packageId, snapshot) in context.Metadata)
        {
            if (!snapshot.IsDeprecated || context.IsIgnored(packageId))
            {
                continue;
            }

            var reasons = snapshot.DeprecationReasons.Count > 0
                ? string.Join(", ", snapshot.DeprecationReasons)
                : "no reason given";

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = DefaultSeverity,
                PackageId = packageId,
                Version = context.EffectiveVersion(packageId),
                Message = $"'{packageId}' is deprecated ({reasons}).",
                Recommendation = snapshot.AlternatePackageId is { } alternate
                    ? $"Migrate to '{alternate}'."
                    : snapshot.DeprecationMessage ?? "Find a maintained replacement.",
            };
        }
    }
}
