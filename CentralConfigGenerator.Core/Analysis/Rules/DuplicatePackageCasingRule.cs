namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Flags package ids that differ only by letter case.</summary>
public sealed class DuplicatePackageCasingRule : IAnalysisRule
{
    public string RuleId => RuleIds.DuplicatePackageCasing;

    public string Title => "Package id casing mismatch";

    public string Description =>
        "The same package is spelled with different capitalisation in different projects.";

    public string Rationale =>
        "NuGet ids are case-insensitive but MSBuild item matching is not always: mismatched "
        + "casing produces duplicate PackageVersion entries and confusing diffs.";

    public Severity DefaultSeverity => Severity.Low;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var groups = context
            .RelevantReferences.Select(r => r.PackageId)
            .Concat(context.CentralVersions.Keys.Where(k => !context.IsIgnored(k)))
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var spellings = group.Distinct(StringComparer.Ordinal).ToList();
            if (spellings.Count < 2)
            {
                continue;
            }

            // Prefer the feed's canonical spelling when it is known.
            var canonical =
                context.MetadataFor(group.Key)?.PackageId
                ?? spellings.OrderBy(s => s, StringComparer.Ordinal).First();

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = DefaultSeverity,
                PackageId = canonical,
                Message =
                    $"'{group.Key}' appears with {spellings.Count} spellings: {string.Join(", ", spellings)}.",
                Recommendation = $"Normalise every reference to '{canonical}'.",
                Fix = FixKind.NormalizePackageCasing,
                FixValue = canonical,
            };
        }
    }
}
