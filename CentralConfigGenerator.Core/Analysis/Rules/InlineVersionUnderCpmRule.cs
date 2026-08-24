namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Flags inline versions that survive after central package management was enabled.</summary>
public sealed class InlineVersionUnderCpmRule : IAnalysisRule
{
    public string RuleId => RuleIds.InlineVersionUnderCpm;

    public string Title => "Inline version under central package management";

    public string Description =>
        "A project still declares Version on a PackageReference even though "
        + "ManagePackageVersionsCentrally is enabled.";

    public string Rationale =>
        "NuGet raises NU1008 for these references, and where it does not, the inline version "
        + "silently wins over the central one — exactly the drift CPM is meant to prevent.";

    public Severity DefaultSeverity => Severity.High;

    public bool RequiresCentralPackageManagement => true;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        foreach (var reference in context.RelevantReferences)
        {
            if (reference.IsGlobal || string.IsNullOrWhiteSpace(reference.Version))
            {
                continue;
            }

            var central = context.CentralVersions.TryGetValue(reference.PackageId, out var value)
                ? value
                : null;

            var drift =
                central is not null
                && !string.Equals(central, reference.Version, StringComparison.OrdinalIgnoreCase);

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = drift ? Severity.High : Severity.Moderate,
                PackageId = reference.PackageId,
                Version = reference.Version,
                ProjectPath = reference.ProjectPath,
                Message = drift
                    ? $"'{reference.PackageId}' pins {reference.Version} inline while Directory.Packages.props says {central}."
                    : $"'{reference.PackageId}' still declares an inline version of {reference.Version}.",
                Recommendation = "Remove the Version attribute and let the central entry apply.",
                Fix = FixKind.RemoveInlineVersion,
                FixValue = central ?? reference.Version,
            };
        }
    }
}
