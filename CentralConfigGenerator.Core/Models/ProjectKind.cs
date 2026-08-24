namespace CentralConfigGenerator.Core.Models;

/// <summary>
/// The kind of MSBuild file that was discovered on disk.
/// </summary>
public enum ProjectKind
{
    Unknown = 0,
    CSharp,
    FSharp,
    VisualBasic,
    Props,
    Targets,
}

public static class ProjectKindExtensions
{
    public static ProjectKind FromPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".csproj" => ProjectKind.CSharp,
            ".fsproj" => ProjectKind.FSharp,
            ".vbproj" => ProjectKind.VisualBasic,
            ".props" => ProjectKind.Props,
            ".targets" => ProjectKind.Targets,
            _ => ProjectKind.Unknown,
        };

    public static bool IsProject(this ProjectKind kind) =>
        kind is ProjectKind.CSharp or ProjectKind.FSharp or ProjectKind.VisualBasic;
}
