namespace CentralConfigGenerator.Core.Analysis;

/// <summary>Aggregated result of a dependency health analysis run.</summary>
public sealed record AnalysisReport
{
    public required IReadOnlyList<Finding> Findings { get; init; }

    public required int ProjectCount { get; init; }

    public required int PackageCount { get; init; }

    public TimeSpan Duration { get; init; }

    /// <summary>True when part of the scan could not complete (for example the feed was unreachable).</summary>
    public bool IsIncomplete { get; init; }

    public IReadOnlyList<string> Incompletions { get; init; } = [];

    public string RootDirectory { get; init; } = string.Empty;

    public int SuppressedCount { get; init; }

    public Severity HighestSeverity =>
        Findings.Count == 0 ? Severity.Never : Findings.Max(f => f.Severity);

    public int CountOf(Severity severity) => Findings.Count(f => f.Severity == severity);

    /// <summary>
    /// A 0-100 dependency health score. Every finding subtracts a weight scaled by severity,
    /// so a clean workspace scores 100.
    /// </summary>
    public int HealthScore
    {
        get
        {
            if (PackageCount == 0)
            {
                return 100;
            }

            double penalty = 0;
            foreach (var finding in Findings)
            {
                penalty += finding.Severity switch
                {
                    Severity.Critical => 25,
                    Severity.High => 12,
                    Severity.Moderate => 5,
                    Severity.Low => 2,
                    Severity.Info => 0.5,
                    _ => 0,
                };
            }

            // Normalise against workspace size so large solutions are not punished twice.
            var scale = Math.Max(1.0, Math.Sqrt(PackageCount) / 3.0);
            var score = 100 - (penalty / scale);
            return (int)Math.Round(Math.Clamp(score, 0, 100));
        }
    }

    public string Grade =>
        HealthScore switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            >= 60 => "D",
            _ => "F",
        };
}
