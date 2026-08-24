namespace CentralConfigGenerator.Core.Analysis;

/// <summary>Controls which analyzers run and how strict the run is.</summary>
public sealed record AnalysisOptions
{
    public string RootDirectory { get; init; } = Directory.GetCurrentDirectory();

    public string? SolutionPath { get; init; }

    public string? ProjectPath { get; init; }

    public string? ExcludePattern { get; init; }

    /// <summary>Query the feed for known security advisories.</summary>
    public bool Audit { get; init; }

    /// <summary>Report packages that are behind the newest published version.</summary>
    public bool Outdated { get; init; }

    /// <summary>Report packages the author marked deprecated.</summary>
    public bool Deprecated { get; init; }

    /// <summary>Flag copyleft and unknown licences.</summary>
    public bool Licenses { get; init; }

    /// <summary>Resolve the transitive graph via <c>dotnet list package</c>.</summary>
    public bool Transitive { get; init; }

    public int MaxParallelism { get; init; } = 8;

    public Severity FailOn { get; init; } = Severity.Info;

    /// <summary>Per-rule severity overrides. A <c>null</c> value disables the rule entirely.</summary>
    public IReadOnlyDictionary<string, Severity?> RuleOverrides { get; init; } =
        new Dictionary<string, Severity?>(StringComparer.OrdinalIgnoreCase);

    public string? BaselinePath { get; init; }

    public IReadOnlyCollection<string> IgnorePackages { get; init; } = [];

    public bool IncludePrerelease { get; init; }

    /// <summary>True when any rule that needs the network is enabled.</summary>
    public bool RequiresNetwork => Audit || Outdated || Deprecated || Licenses;
}
