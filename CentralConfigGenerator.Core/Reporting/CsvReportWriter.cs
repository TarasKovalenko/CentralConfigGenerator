using System.Text;
using CentralConfigGenerator.Core.Analysis;

namespace CentralConfigGenerator.Core.Reporting;

/// <summary>One row per finding, for spreadsheets and ad-hoc pivots.</summary>
public sealed class CsvReportWriter : IReportWriter
{
    public ReportFormat Format => ReportFormat.Csv;

    public string Write(AnalysisReport report)
    {
        var builder = new StringBuilder();
        builder.Append("RuleId,Severity,PackageId,Version,Project,Message,Recommendation,AutoFixable,Fingerprint\n");

        foreach (var finding in report.Findings)
        {
            builder
                .Append(Quote(finding.RuleId)).Append(',')
                .Append(Quote(finding.Severity.ToString())).Append(',')
                .Append(Quote(finding.PackageId)).Append(',')
                .Append(Quote(finding.Version)).Append(',')
                .Append(Quote(ReportPaths.Relative(finding.ProjectPath, report.RootDirectory))).Append(',')
                .Append(Quote(finding.Message)).Append(',')
                .Append(Quote(finding.Recommendation)).Append(',')
                .Append(finding.IsAutoFixable ? "true" : "false").Append(',')
                .Append(Quote(finding.Fingerprint)).Append('\n');
        }

        return builder.ToString();
    }

    private static string Quote(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
        return $"\"{escaped}\"";
    }
}
