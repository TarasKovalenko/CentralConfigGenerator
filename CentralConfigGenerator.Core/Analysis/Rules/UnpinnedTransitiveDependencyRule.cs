namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>
/// Reports vulnerable or deprecated transitive dependencies that no central entry pins,
/// so they cannot be fixed without upgrading their parent.
/// </summary>
public sealed class UnpinnedTransitiveDependencyRule : IAnalysisRule
{
    public string RuleId => RuleIds.UnpinnedTransitiveDependency;

    public string Title => "Risky transitive dependency is not pinned";

    public string Description =>
        "A transitive dependency carries an advisory or deprecation notice but has no "
        + "PackageVersion entry that could override it.";

    public string Rationale =>
        "Transitive pinning is the only way to patch a vulnerable indirect dependency without "
        + "waiting for the intermediate package to publish a fix.";

    public Severity DefaultSeverity => Severity.High;

    public bool RequiresNetwork => true;

    public bool IsEnabled(AnalysisOptions options) => options.Transitive && options.Audit;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        if (!context.Graph.IsAvailable)
        {
            yield break;
        }

        var directIds = context
            .References.Select(r => r.PackageId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var package in context.Graph.Transitive)
        {
            if (
                directIds.Contains(package.PackageId)
                || context.CentralVersions.ContainsKey(package.PackageId)
                || context.IsIgnored(package.PackageId)
                || !seen.Add(package.PackageId)
            )
            {
                continue;
            }

            var snapshot = context.MetadataFor(package.PackageId);
            if (snapshot is null)
            {
                continue;
            }

            var affected = snapshot.Vulnerabilities.Any(v =>
                string.Equals(v.AffectedRange, package.ResolvedVersion, StringComparison.OrdinalIgnoreCase)
            );

            if (!affected && !snapshot.IsDeprecated)
            {
                continue;
            }

            var safe = snapshot
                .AllVersions.Where(v =>
                    !v.IsPrerelease
                    && v.ToNormalizedString() != package.ResolvedVersion
                    && !snapshot.Vulnerabilities.Any(x =>
                        string.Equals(
                            x.AffectedRange,
                            v.ToNormalizedString(),
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                )
                .OrderByDescending(v => v)
                .FirstOrDefault();

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = affected ? Severity.High : Severity.Moderate,
                PackageId = package.PackageId,
                Version = package.ResolvedVersion,
                ProjectPath = package.ProjectPath,
                Message =
                    $"Transitive '{package.PackageId}' {package.ResolvedVersion} is "
                    + (affected ? "vulnerable" : "deprecated")
                    + " and is not pinned centrally.",
                Recommendation = safe is null
                    ? "Enable CentralPackageTransitivePinningEnabled and pin a safe version."
                    : $"Pin '{package.PackageId}' to {safe.ToNormalizedString()} with transitive pinning enabled.",
                Fix = safe is null ? FixKind.None : FixKind.SetCentralVersion,
                FixValue = safe?.ToNormalizedString(),
            };
        }
    }
}
