using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CentralConfigGenerator.Core.Analysis;

namespace CentralConfigGenerator.Core.Reporting;

/// <summary>Machine-readable report; the shape is stable and safe to script against.</summary>
public sealed class JsonReportWriter : IReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public ReportFormat Format => ReportFormat.Json;

    public string Write(AnalysisReport report)
    {
        var payload = new
        {
            success = report.HighestSeverity < Severity.High,
            summary = new
            {
                healthScore = report.HealthScore,
                grade = report.Grade,
                projectCount = report.ProjectCount,
                packageCount = report.PackageCount,
                findingCount = report.Findings.Count,
                suppressedCount = report.SuppressedCount,
                durationMs = (long)report.Duration.TotalMilliseconds,
                incomplete = report.IsIncomplete,
                bySeverity = new
                {
                    critical = report.CountOf(Severity.Critical),
                    high = report.CountOf(Severity.High),
                    moderate = report.CountOf(Severity.Moderate),
                    low = report.CountOf(Severity.Low),
                    info = report.CountOf(Severity.Info),
                },
                byRule = report
                    .Findings.GroupBy(f => f.RuleId)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count()),
            },
            incompletions = report.Incompletions,
            findings = report
                .Findings.Select(f => new
                {
                    ruleId = f.RuleId,
                    severity = f.Severity.ToString(),
                    message = f.Message,
                    packageId = f.PackageId,
                    version = f.Version,
                    project = ReportPaths.Relative(f.ProjectPath, report.RootDirectory),
                    recommendation = f.Recommendation,
                    autoFixable = f.IsAutoFixable,
                    fingerprint = f.Fingerprint,
                })
                .ToList(),
        };

        return JsonSerializer.Serialize(payload, Options);
    }
}
