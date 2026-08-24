using CentralConfigGenerator.Core.Analysis.Rules;

namespace CentralConfigGenerator.Core.Analysis;

/// <summary>The complete set of analysis rules, and lookups used by the <c>explain</c> command.</summary>
public static class RuleCatalog
{
    public static IReadOnlyList<IAnalysisRule> CreateAll() =>
    [
        new SecurityVulnerabilityRule(),
        new InlineVersionUnderCpmRule(),
        new MissingCentralVersionRule(),
        new DeprecatedPackageRule(),
        new LicenseRiskRule(),
        new VersionInconsistencyRule(),
        new TransitiveConflictRule(),
        new UnpinnedTransitiveDependencyRule(),
        new FloatingVersionRule(),
        new RedundantReferenceRule(),
        new DuplicatePackageCasingRule(),
        new OutdatedPackageRule(),
        new PrereleaseInProductionRule(),
        new OrphanedPackageVersionRule(),
        new FrameworkAlignmentRule(),
        new PropertyDriftRule(),
    ];

    public static IAnalysisRule? Find(string ruleId) =>
        CreateAll()
            .FirstOrDefault(r => string.Equals(r.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));
}
