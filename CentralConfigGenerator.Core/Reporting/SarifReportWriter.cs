using System.Text.Encodings.Web;
using System.Text.Json;
using CentralConfigGenerator.Core.Analysis;

namespace CentralConfigGenerator.Core.Reporting;

/// <summary>
/// SARIF 2.1.0 output, so findings render as inline annotations when uploaded with
/// <c>github/codeql-action/upload-sarif</c>.
/// </summary>
public sealed class SarifReportWriter : IReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public ReportFormat Format => ReportFormat.Sarif;

    public string Write(AnalysisReport report)
    {
        var usedRules = report
            .Findings.Select(f => f.RuleId)
            .Distinct(StringComparer.Ordinal)
            .Select(RuleCatalog.Find)
            .Where(r => r is not null)
            .Select(r => new
            {
                id = r!.RuleId,
                name = r.Title,
                shortDescription = new { text = r.Title },
                fullDescription = new { text = r.Description },
                help = new { text = r.Rationale, markdown = $"**{r.Title}**\n\n{r.Description}\n\n{r.Rationale}" },
                defaultConfiguration = new { level = ToSarifLevel(r.DefaultSeverity) },
                properties = new { tags = new[] { "dependencies", "nuget" } },
            })
            .ToList();

        var payload = new
        {
            version = "2.1.0",
            schema = "https://json.schemastore.org/sarif-2.1.0.json",
            runs = new[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = "CentralConfigGenerator",
                            informationUri = "https://github.com/TarasKovalenko/CentralConfigGenerator",
                            version = ToolVersion,
                            rules = usedRules,
                        },
                    },
                    results = report
                        .Findings.Select(f => new
                        {
                            ruleId = f.RuleId,
                            level = ToSarifLevel(f.Severity),
                            message = new
                            {
                                text = f.Recommendation is null
                                    ? f.Message
                                    : $"{f.Message} {f.Recommendation}",
                            },
                            partialFingerprints = new { centralConfig = f.Fingerprint },
                            locations = new[]
                            {
                                new
                                {
                                    physicalLocation = new
                                    {
                                        artifactLocation = new
                                        {
                                            uri = ToUri(f.ProjectPath, report.RootDirectory),
                                        },
                                        region = new { startLine = Math.Max(1, f.Line) },
                                    },
                                },
                            },
                        })
                        .ToList(),
                },
            },
        };

        // The SARIF schema property is "$schema", which C# cannot express as an identifier.
        return JsonSerializer.Serialize(payload, Options).Replace("\"schema\":", "\"$schema\":");
    }

    private static string ToolVersion =>
        typeof(SarifReportWriter).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    private static string ToSarifLevel(Severity severity) =>
        severity switch
        {
            Severity.Critical or Severity.High => "error",
            Severity.Moderate => "warning",
            Severity.Low => "note",
            _ => "note",
        };

    private static string ToUri(string? path, string root) =>
        ReportPaths.Relative(
            path,
            string.IsNullOrWhiteSpace(root) ? Directory.GetCurrentDirectory() : root
        ) ?? "Directory.Packages.props";
}
