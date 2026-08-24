namespace CentralConfigGenerator.Core.Models;

/// <summary>Process exit codes, stable across releases so CI can branch on them.</summary>
public static class ExitCode
{
    public const int Success = 0;
    public const int ValidationError = 1;
    public const int FileOperationError = 2;
    public const int VersionConflict = 3;
    public const int NoProjectsFound = 4;
    public const int AnalysisIssuesFound = 5;
    public const int UnexpectedError = 6;
    public const int TestFailure = 7;
    public const int IncompleteAnalysis = 8;
    public const int GraphDrift = 9;

    public static string Describe(int code) =>
        code switch
        {
            Success => "Success",
            ValidationError => "ValidationError",
            FileOperationError => "FileOperationError",
            VersionConflict => "VersionConflict",
            NoProjectsFound => "NoProjectsFound",
            AnalysisIssuesFound => "AnalysisIssuesFound",
            UnexpectedError => "UnexpectedError",
            TestFailure => "TestFailure",
            IncompleteAnalysis => "IncompleteAnalysis",
            GraphDrift => "GraphDrift",
            _ => "Unknown",
        };
}
