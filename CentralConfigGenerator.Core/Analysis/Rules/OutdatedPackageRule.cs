using NuGet.Versioning;

namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Reports packages that lag behind the newest published version.</summary>
public sealed class OutdatedPackageRule : IAnalysisRule
{
    public string RuleId => RuleIds.OutdatedPackage;

    public string Title => "Outdated package";

    public string Description => "A newer version of the package is published on the feed.";

    public string Rationale =>
        "Severity scales with the size of the gap: a patch behind is noise, a major behind is a "
        + "migration that only gets harder to do later.";

    public Severity DefaultSeverity => Severity.Low;

    public bool RequiresNetwork => true;

    public bool IsEnabled(AnalysisOptions options) => options.Outdated;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        foreach (var (packageId, snapshot) in context.Metadata)
        {
            if (context.IsIgnored(packageId) || !snapshot.Found)
            {
                continue;
            }

            var current = context.EffectiveVersion(packageId);
            if (current is null || !NuGetVersion.TryParse(current, out var currentVersion))
            {
                continue;
            }

            var latest = context.Options.IncludePrerelease
                ? snapshot.LatestIncludingPrerelease
                : snapshot.LatestStable;

            if (latest is null || latest <= currentVersion)
            {
                continue;
            }

            var majorGap = latest.Major - currentVersion.Major;
            var severity = majorGap switch
            {
                >= 2 => Severity.Moderate,
                1 => Severity.Low,
                _ => latest.Minor > currentVersion.Minor ? Severity.Low : Severity.Info,
            };

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = severity,
                PackageId = packageId,
                Version = current,
                Message =
                    $"'{packageId}' is on {current}; {latest.ToNormalizedString()} is available"
                    + (majorGap > 0 ? $" ({majorGap} major version(s) behind)." : "."),
                Recommendation = $"Update to {latest.ToNormalizedString()}.",
                Fix = FixKind.SetCentralVersion,
                FixValue = latest.ToNormalizedString(),
            };
        }
    }
}
