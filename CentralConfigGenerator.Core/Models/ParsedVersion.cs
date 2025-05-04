using NuGet.Versioning;

namespace CentralConfigGenerator.Core.Models;

public record ParsedVersion
{
    public required string Original { get; init; }
    public NuGetVersion? Parsed { get; init; }
    public VersionRange? Range { get; init; }
}