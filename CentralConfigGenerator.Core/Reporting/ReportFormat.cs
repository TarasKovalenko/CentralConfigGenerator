namespace CentralConfigGenerator.Core.Reporting;

/// <summary>Output formats supported by every reporting command.</summary>
public enum ReportFormat
{
    Terminal,
    Json,
    Sarif,
    Markdown,
    Csv,
}
