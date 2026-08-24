using System.Text.Json;
using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Reporting;

namespace CentralConfigGenerator.Core.Tests.Reporting;

public class ReportWriterTests
{
    private static readonly AnalysisReport Report = new()
    {
        ProjectCount = 2,
        PackageCount = 3,
        RootDirectory = Path.Combine(Path.GetTempPath(), "ccg-report"),
        Duration = TimeSpan.FromSeconds(1.5),
        Findings =
        [
            new Finding
            {
                RuleId = RuleIds.SecurityVulnerability,
                Severity = Severity.Critical,
                Message = "'Pkg' 1.0.0 has a known advisory.",
                PackageId = "Pkg",
                Version = "1.0.0",
                ProjectPath = Path.Combine(Path.GetTempPath(), "ccg-report", "src", "A.csproj"),
                Recommendation = "Upgrade to 1.0.1.",
                Fix = FixKind.SetCentralVersion,
                FixValue = "1.0.1",
            },
            new Finding
            {
                RuleId = RuleIds.FloatingVersion,
                Severity = Severity.Moderate,
                Message = "'xunit' uses a floating version.",
                PackageId = "xunit",
            },
        ],
    };

    [Fact]
    public void Json_ShouldProduceParseableOutputWithASummary()
    {
        var json = new JsonReportWriter().Write(Report);

        using var document = JsonDocument.Parse(json);
        var summary = document.RootElement.GetProperty("summary");

        summary.GetProperty("projectCount").GetInt32().ShouldBe(2);
        summary.GetProperty("bySeverity").GetProperty("critical").GetInt32().ShouldBe(1);
        document.RootElement.GetProperty("findings").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public void Json_ShouldNotEscapeApostrophes() =>
        new JsonReportWriter().Write(Report).ShouldContain("'Pkg'");

    [Fact]
    public void Json_ShouldEmitWorkspaceRelativeProjectPaths()
    {
        var json = new JsonReportWriter().Write(Report);

        json.ShouldContain("src/A.csproj");
    }

    [Fact]
    public void Sarif_ShouldEmitValidSchemaAndRules()
    {
        var sarif = new SarifReportWriter().Write(Report);

        using var document = JsonDocument.Parse(sarif);
        document.RootElement.GetProperty("version").GetString().ShouldBe("2.1.0");
        document.RootElement.TryGetProperty("$schema", out _).ShouldBeTrue();

        var run = document.RootElement.GetProperty("runs")[0];
        run.GetProperty("results").GetArrayLength().ShouldBe(2);
        run.GetProperty("tool").GetProperty("driver").GetProperty("rules").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public void Sarif_ShouldMapCriticalToError()
    {
        var sarif = new SarifReportWriter().Write(Report);

        using var document = JsonDocument.Parse(sarif);
        document
            .RootElement.GetProperty("runs")[0]
            .GetProperty("results")[0]
            .GetProperty("level")
            .GetString()
            .ShouldBe("error");
    }

    [Fact]
    public void Markdown_ShouldLeadWithTheVerdict()
    {
        var markdown = new MarkdownReportWriter().Write(Report);

        markdown.ShouldStartWith("## Dependency health:");
        markdown.ShouldContain("Critical issues found");
        markdown.ShouldContain("<details");
    }

    [Fact]
    public void Markdown_ShouldSayWhenThereIsNothingToReport()
    {
        var clean = Report with { Findings = [] };

        new MarkdownReportWriter().Write(clean).ShouldContain("No findings");
    }

    [Fact]
    public void Csv_ShouldWriteAHeaderAndOneRowPerFinding()
    {
        var csv = new CsvReportWriter().Write(Report);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines[0].ShouldStartWith("RuleId,Severity");
        lines.Length.ShouldBe(3);
    }

    [Fact]
    public void Csv_ShouldQuoteEmbeddedQuotes()
    {
        var report = Report with
        {
            Findings =
            [
                new Finding
                {
                    RuleId = "X",
                    Severity = Severity.Low,
                    Message = "he said \"hi\"",
                },
            ],
        };

        new CsvReportWriter().Write(report).ShouldContain("\"he said \"\"hi\"\"\"");
    }

    [Theory]
    [InlineData(ReportFormat.Json)]
    [InlineData(ReportFormat.Sarif)]
    [InlineData(ReportFormat.Markdown)]
    [InlineData(ReportFormat.Csv)]
    public void Factory_ShouldReturnAWriterForEveryFormat(ReportFormat format) =>
        ReportWriterFactory.Create(format).Format.ShouldBe(format);
}
