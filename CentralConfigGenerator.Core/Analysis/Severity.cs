namespace CentralConfigGenerator.Core.Analysis;

/// <summary>Finding severity, ordered so thresholds can be compared numerically.</summary>
public enum Severity
{
    /// <summary>Used only as a <c>--fail-on</c> threshold meaning "never fail".</summary>
    Never = 0,
    Info = 1,
    Low = 2,
    Moderate = 3,
    High = 4,
    Critical = 5,
}
