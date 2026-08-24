using CentralConfigGenerator.Core.Services;

namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Flags pre-release versions outside of test-only packages.</summary>
public sealed class PrereleaseInProductionRule : IAnalysisRule
{
    public string RuleId => RuleIds.PrereleaseInProduction;

    public string Title => "Pre-release dependency";

    public string Description => "A package resolves to a pre-release version.";

    public string Rationale =>
        "Pre-release packages can be unlisted or replaced by the author without notice, and their "
        + "API is not covered by any compatibility promise.";

    public Severity DefaultSeverity => Severity.Low;

    public bool IsEnabled(AnalysisOptions options) => !options.IncludePrerelease;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in context.RelevantReferences)
        {
            var version = reference.Version ?? context.EffectiveVersion(reference.PackageId);

            if (
                version is null
                || !VersionSelector.IsPrerelease(version)
                || !reported.Add(reference.PackageId)
            )
            {
                continue;
            }

            var stable = context.MetadataFor(reference.PackageId)?.LatestStable;

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = DefaultSeverity,
                PackageId = reference.PackageId,
                Version = version,
                ProjectPath = reference.ProjectPath,
                Message = $"'{reference.PackageId}' uses pre-release version {version}.",
                Recommendation = stable is null
                    ? "Move to a stable release before shipping."
                    : $"A stable {stable.ToNormalizedString()} is available.",
                Fix = stable is null ? FixKind.None : FixKind.SetCentralVersion,
                FixValue = stable?.ToNormalizedString(),
            };
        }
    }
}
