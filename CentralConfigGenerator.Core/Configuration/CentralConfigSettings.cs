using System.Text.Json.Serialization;
using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Configuration;

/// <summary>
/// Contents of a <c>.centralconfig.json</c> file. Every value is optional; anything left unset
/// falls back to the command-line default.
/// </summary>
public sealed record CentralConfigSettings
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }

    public VersionResolutionStrategyName? ConflictStrategy { get; init; }

    public bool? Backup { get; init; }

    public string? BackupDir { get; init; }

    public bool? AddGitignore { get; init; }

    public Severity? FailOn { get; init; }

    public string? Baseline { get; init; }

    public RetentionSettings? Retention { get; init; }

    public Dictionary<string, string>? Rules { get; init; }

    public string? ExcludeDirs { get; init; }

    public bool? TransitivePinning { get; init; }

    public bool? IgnorePrerelease { get; init; }

    public LineEndingStyle? LineEnding { get; init; }

    public string? Encoding { get; init; }

    /// <summary>Property names promoted into Directory.Build.props by the <c>build</c> command.</summary>
    public List<string>? BuildProperties { get; init; }

    /// <summary>Packages that analysis should ignore entirely.</summary>
    public List<string>? IgnorePackages { get; init; }

    public string? SourceDirectory { get; init; }
}

public sealed record RetentionSettings
{
    public bool? Enabled { get; init; }

    public int? MaxBackups { get; init; }
}

/// <summary>Serialisable mirror of <c>VersionResolutionStrategy</c>, including <c>Fail</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VersionResolutionStrategyName
{
    Highest,
    Lowest,
    MostCommon,
    Fail,
    Manual,
}
