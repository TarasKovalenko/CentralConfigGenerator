namespace CentralConfigGenerator.Core.Analysis.Rules;

/// <summary>Flags the same package referenced twice inside one project.</summary>
public sealed class RedundantReferenceRule : IAnalysisRule
{
    public string RuleId => RuleIds.RedundantReference;

    public string Title => "Duplicate package reference";

    public string Description => "One project declares the same PackageReference more than once.";

    public string Rationale =>
        "Duplicates trigger NU1504 warnings and make it ambiguous which metadata (PrivateAssets, "
        + "IncludeAssets) actually applies.";

    public Severity DefaultSeverity => Severity.Moderate;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var groups = context.RelevantReferences.GroupBy(
            r => (r.ProjectPath, r.PackageId),
            TupleComparer.Instance
        );

        foreach (var group in groups)
        {
            var entries = group.ToList();
            if (entries.Count < 2)
            {
                continue;
            }

            // References separated by a condition are legitimate (per-TFM references).
            if (entries.All(e => !string.IsNullOrWhiteSpace(e.Condition))
                && entries.Select(e => e.Condition).Distinct(StringComparer.OrdinalIgnoreCase).Count() == entries.Count)
            {
                continue;
            }

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = DefaultSeverity,
                PackageId = group.Key.PackageId,
                ProjectPath = group.Key.ProjectPath,
                Message =
                    $"'{group.Key.PackageId}' is referenced {entries.Count} times in {Path.GetFileName(group.Key.ProjectPath)}.",
                Recommendation = "Keep a single PackageReference and delete the duplicates.",
                Fix = FixKind.RemoveDuplicateReference,
                FixValue = group.Key.PackageId,
            };
        }
    }

    private sealed class TupleComparer : IEqualityComparer<(string ProjectPath, string PackageId)>
    {
        public static readonly TupleComparer Instance = new();

        public bool Equals(
            (string ProjectPath, string PackageId) x,
            (string ProjectPath, string PackageId) y
        ) =>
            string.Equals(x.ProjectPath, y.ProjectPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.PackageId, y.PackageId, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string ProjectPath, string PackageId) obj) =>
            HashCode.Combine(
                obj.ProjectPath.ToLowerInvariant(),
                obj.PackageId.ToLowerInvariant()
            );
    }
}
