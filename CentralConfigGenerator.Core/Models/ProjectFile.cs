using CentralConfigGenerator.Core.IO;

namespace CentralConfigGenerator.Core.Models;

/// <summary>
/// An MSBuild file that was read from disk, together with everything needed to write it back
/// without disturbing its original encoding or line endings.
/// </summary>
public record ProjectFile
{
    public string Path { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    /// <summary>The kind of MSBuild file, inferred from the extension when not set explicitly.</summary>
    public ProjectKind Kind { get; set; } = ProjectKind.Unknown;

    /// <summary>The original on-disk text format. Defaults to BOM-less UTF-8.</summary>
    public TextFileFormat Format { get; set; } = TextFileFormat.Default;

    public ProjectKind ResolvedKind =>
        Kind != ProjectKind.Unknown ? Kind : ProjectKindExtensions.FromPath(Path);

    public string FileName => System.IO.Path.GetFileName(Path);

    public string DirectoryPath => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
}
