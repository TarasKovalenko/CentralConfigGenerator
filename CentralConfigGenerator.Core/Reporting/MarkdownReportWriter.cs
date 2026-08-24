using System.Text;
using CentralConfigGenerator.Core.Analysis;

namespace CentralConfigGenerator.Core.Reporting;

/// <summary>Verdict-first Markdown, sized for a GitHub step summary or a PR comment.</summary>
public sealed class MarkdownReportWriter : IReportWriter
{
    public ReportFormat Format => ReportFormat.Markdown;

    public string Write(AnalysisReport report)
    {
        var builder = new StringBuilder();
        var verdict = report.HighestSeverity switch
        {
            Severity.Critical => "❌ Critical issues found",
            Severity.High => "❌ High severity issues found",
            Severity.Moderate => "⚠️ Moderate issues found",
            Severity.Low or Severity.Info => "✅ Minor findings only",
            _ => "✅ No issues found",
        };

        builder.Append("## Dependency health: ").Append(report.HealthScore).Append("/100 (")
            .Append(report.Grade).Append(")\n\n");
        builder.Append(verdict).Append("\n\n");

        builder.Append("| Metric | Value |\n|---|---|\n");
        builder.Append("| Projects | ").Append(report.ProjectCount).Append(" |\n");
        builder.Append("| Packages | ").Append(report.PackageCount).Append(" |\n");
        builder.Append("| Findings | ").Append(report.Findings.Count).Append(" |\n");

        if (report.SuppressedCount > 0)
        {
            builder.Append("| Suppressed by baseline | ").Append(report.SuppressedCount).Append(" |\n");
        }

        builder.Append("| Duration | ").Append($"{report.Duration.TotalSeconds:F1}s").Append(" |\n\n");

        if (report.Findings.Count == 0)
        {
            builder.Append("No findings. 🎉\n");
            return builder.ToString();
        }

        builder.Append("| Severity | Count |\n|---|---|\n");
        foreach (var severity in new[] { Severity.Critical, Severity.High, Severity.Moderate, Severity.Low, Severity.Info })
        {
            var count = report.CountOf(severity);
            if (count > 0)
            {
                builder.Append("| ").Append(Icon(severity)).Append(' ').Append(severity)
                    .Append(" | ").Append(count).Append(" |\n");
            }
        }

        builder.Append('\n');

        foreach (var group in report.Findings.GroupBy(f => f.Severity).OrderByDescending(g => g.Key))
        {
            builder.Append("<details");
            if (group.Key >= Severity.High)
            {
                builder.Append(" open");
            }

            builder.Append("><summary>").Append(Icon(group.Key)).Append(' ').Append(group.Key)
                .Append(" (").Append(group.Count()).Append(")</summary>\n\n");

            foreach (var finding in group)
            {
                builder.Append("- **").Append(finding.RuleId).Append("** — ")
                    .Append(Escape(finding.Message));

                if (finding.Recommendation is not null)
                {
                    builder.Append(' ').Append(Escape(finding.Recommendation));
                }

                if (finding.ProjectPath is not null)
                {
                    builder.Append(" `").Append(Path.GetFileName(finding.ProjectPath)).Append('`');
                }

                builder.Append('\n');
            }

            builder.Append("\n</details>\n\n");
        }

        if (report.IsIncomplete)
        {
            builder.Append("> ⚠️ The scan did not complete fully:\n");
            foreach (var incompletion in report.Incompletions)
            {
                builder.Append("> - ").Append(Escape(incompletion)).Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string Icon(Severity severity) =>
        severity switch
        {
            Severity.Critical => "🔴",
            Severity.High => "🟠",
            Severity.Moderate => "🟡",
            Severity.Low => "🔵",
            _ => "⚪",
        };

    private static string Escape(string value) => value.Replace("|", "\\|");
}
