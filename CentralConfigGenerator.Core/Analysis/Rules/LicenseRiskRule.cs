namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Flags copyleft, proprietary and unknown licences.</summary>
public sealed class LicenseRiskRule : IAnalysisRule
{
    /// <summary>Licence identifiers that impose reciprocal obligations on distributed software.</summary>
    private static readonly string[] StrongCopyleft =
    [
        "GPL-2.0",
        "GPL-3.0",
        "AGPL-3.0",
        "SSPL",
        "OSL-3.0",
        "EUPL",
    ];

    private static readonly string[] WeakCopyleft = ["LGPL", "MPL-2.0", "MS-RL", "CDDL", "EPL"];

    public string RuleId => RuleIds.LicenseRisk;

    public string Title => "Licence risk";

    public string Description =>
        "A dependency ships under a copyleft, proprietary or undeclared licence.";

    public string Rationale =>
        "Copyleft terms can propagate to the software that links the package, and an undeclared "
        + "licence means no permission has actually been granted. Both need a human decision.";

    public Severity DefaultSeverity => Severity.Moderate;

    public bool RequiresNetwork => true;

    public bool IsEnabled(AnalysisOptions options) => options.Licenses;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        foreach (var (packageId, snapshot) in context.Metadata)
        {
            if (context.IsIgnored(packageId) || !snapshot.Found)
            {
                continue;
            }

            var license = snapshot.License;

            if (string.IsNullOrWhiteSpace(license))
            {
                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Low,
                    PackageId = packageId,
                    Version = context.EffectiveVersion(packageId),
                    Message = $"'{packageId}' does not declare a licence.",
                    Recommendation =
                        "Confirm the terms with the author before shipping this dependency.",
                };
                continue;
            }

            if (StrongCopyleft.Any(l => license.Contains(l, StringComparison.OrdinalIgnoreCase)))
            {
                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.High,
                    PackageId = packageId,
                    Version = context.EffectiveVersion(packageId),
                    Message = $"'{packageId}' is licensed under {license} (strong copyleft).",
                    Recommendation =
                        "Distributing linked software may require releasing your source. Get legal sign-off.",
                };
                continue;
            }

            if (WeakCopyleft.Any(l => license.Contains(l, StringComparison.OrdinalIgnoreCase)))
            {
                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Moderate,
                    PackageId = packageId,
                    Version = context.EffectiveVersion(packageId),
                    Message = $"'{packageId}' is licensed under {license} (weak copyleft).",
                    Recommendation =
                        "Modifications to the package itself must usually be published. Keep it unforked.",
                };
                continue;
            }

            if (license.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Info,
                    PackageId = packageId,
                    Version = context.EffectiveVersion(packageId),
                    Message = $"'{packageId}' points at a licence URL rather than an SPDX expression.",
                    Recommendation = $"Review {license} manually.",
                };
            }
        }
    }
}
