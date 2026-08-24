using System.Text.Json.Serialization;

namespace CentralConfigGenerator.Core.Models;

/// <summary>A single timestamped backup produced before a mutating operation.</summary>
public sealed record BackupSet
{
    public required string Id { get; init; }

    public required string Path { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required string Operation { get; init; }

    public required IReadOnlyList<string> Files { get; init; }

    [JsonIgnore]
    public long SizeInBytes { get; init; }
}

/// <summary>Manifest persisted alongside the copied files inside a backup directory.</summary>
public sealed record BackupManifest
{
    public string Id { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public string Operation { get; init; } = string.Empty;

    public string RootDirectory { get; init; } = string.Empty;

    public List<BackupEntry> Entries { get; init; } = [];
}

public sealed record BackupEntry
{
    public string OriginalPath { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    /// <summary>True when the file did not exist before the operation and should be deleted on rollback.</summary>
    public bool WasCreated { get; init; }
}
