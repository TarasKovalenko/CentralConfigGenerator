namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Flags PackageVersion entries that no project references any more.</summary>
public sealed class OrphanedPackageVersionRule : IAnalysisRule
{
    public string RuleId => RuleIds.OrphanedPackageVersion;

    public string Title => "Unused central version entry";

    public string Description =>
        "Directory.Packages.props pins a package that no project references.";

    public string Rationale =>
        "Dead entries accumulate after refactors and make the central file look like it "
        + "describes dependencies that no longer exist. They also confuse audit tooling.";

    public Severity DefaultSeverity => Severity.Info;

    public bool RequiresCentralPackageManagement => true;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var referenced = context
            .References.Select(r => r.PackageId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (packageId, version) in context.CentralVersions)
        {
            if (referenced.Contains(packageId) || context.IsIgnored(packageId))
            {
                continue;
            }

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = DefaultSeverity,
                PackageId = packageId,
                Version = version,
                ProjectPath = context.PackagesPropsPath,
                Message = $"'{packageId}' is pinned centrally but referenced by no project.",
                Recommendation = "Remove the PackageVersion entry.",
                Fix = FixKind.RemoveOrphanedPackageVersion,
                FixValue = packageId,
            };
        }
    }
}
