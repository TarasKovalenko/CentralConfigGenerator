using CentralConfigGenerator.Core.Analysis;

namespace CentralConfigGenerator.Core.Reporting;

public interface IReportWriter
{
    ReportFormat Format { get; }

    string Write(AnalysisReport report);
}

public static class ReportWriterFactory
{
    public static IReportWriter Create(ReportFormat format) =>
        format switch
        {
            ReportFormat.Json => new JsonReportWriter(),
            ReportFormat.Sarif => new SarifReportWriter(),
            ReportFormat.Markdown => new MarkdownReportWriter(),
            ReportFormat.Csv => new CsvReportWriter(),
            _ => new JsonReportWriter(),
        };
}
