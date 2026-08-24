namespace CentralConfigGenerator.Core.Analysis;

/// <summary>One dependency-health check.</summary>
public interface IAnalysisRule
{
    string RuleId { get; }

    string Title { get; }

    /// <summary>What the rule looks for, shown by the <c>explain</c> command.</summary>
    string Description { get; }

    /// <summary>Why it matters and what to do about it.</summary>
    string Rationale { get; }

    Severity DefaultSeverity { get; }

    /// <summary>True when the rule needs feed metadata to produce results.</summary>
    bool RequiresNetwork => false;

    /// <summary>True when the rule only applies once central package management is in play.</summary>
    bool RequiresCentralPackageManagement => false;

    bool IsEnabled(AnalysisOptions options) => true;

    IEnumerable<Finding> Analyze(AnalysisContext context);
}
