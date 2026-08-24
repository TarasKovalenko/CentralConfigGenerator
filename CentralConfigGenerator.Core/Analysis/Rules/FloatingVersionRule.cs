using CentralConfigGenerator.Core.Services;

namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Flags wildcard and open-ended version specifications.</summary>
public sealed class FloatingVersionRule : IAnalysisRule
{
    public string RuleId => RuleIds.FloatingVersion;

    public string Title => "Floating version";

    public string Description =>
        "A version uses a wildcard (4.*) or an unbounded range ([4.0.0,)).";

    public string Rationale =>
        "Floating versions make builds non-reproducible: the same commit resolves to different "
        + "binaries depending on when it was restored, which turns a feed publish into a surprise "
        + "production change.";

    public Severity DefaultSeverity => Severity.Moderate;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (packageId, version) in AllVersions(context))
        {
            if (!VersionSelector.IsFloating(version) || !seen.Add($"{packageId}|{version}"))
            {
                continue;
            }

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = DefaultSeverity,
                PackageId = packageId,
                Version = version,
                Message = $"'{packageId}' uses the floating version '{version}'.",
                Recommendation =
                    "Pin an exact version so restores are reproducible across machines and time.",
            };
        }
    }

    private static IEnumerable<(string PackageId, string Version)> AllVersions(
        AnalysisContext context
    )
    {
        foreach (var reference in context.RelevantReferences)
        {
            if (!string.IsNullOrWhiteSpace(reference.Version))
            {
                yield return (reference.PackageId, reference.Version!);
            }
        }

        foreach (var (packageId, version) in context.CentralVersions)
        {
            if (!context.IsIgnored(packageId))
            {
                yield return (packageId, version);
            }
        }
    }
}
