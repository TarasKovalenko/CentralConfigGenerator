namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Flags references that have neither an inline nor a central version.</summary>
public sealed class MissingCentralVersionRule : IAnalysisRule
{
    public string RuleId => RuleIds.MissingCentralVersion;

    public string Title => "Package has no version anywhere";

    public string Description =>
        "A PackageReference has no inline version and no matching PackageVersion entry.";

    public string Rationale =>
        "The restore fails with NU1010. It usually means a package was added by hand after the "
        + "migration, or a PackageVersion entry was deleted.";

    public Severity DefaultSeverity => Severity.Critical;

    public bool RequiresCentralPackageManagement => true;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        foreach (var reference in context.RelevantReferences)
        {
            if (
                reference.IsGlobal
                || !string.IsNullOrWhiteSpace(reference.Version)
                || context.CentralVersions.ContainsKey(reference.PackageId)
            )
            {
                continue;
            }

            var suggested = context
                .MetadataFor(reference.PackageId)
                ?.LatestStable?.ToNormalizedString();

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = DefaultSeverity,
                PackageId = reference.PackageId,
                ProjectPath = reference.ProjectPath,
                Message =
                    $"'{reference.PackageId}' has no version: no inline attribute and no PackageVersion entry.",
                Recommendation = suggested is null
                    ? "Add a PackageVersion entry to Directory.Packages.props."
                    : $"Add <PackageVersion Include=\"{reference.PackageId}\" Version=\"{suggested}\" />.",
                Fix = suggested is null ? FixKind.None : FixKind.SetCentralVersion,
                FixValue = suggested,
            };
        }
    }
}
