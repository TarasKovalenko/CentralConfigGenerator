using System.Text.Json;
using CentralConfigGenerator.Core.IO;

namespace CentralConfigGenerator.Core.Analysis;

/// <summary>A recorded set of accepted findings, used to keep CI green while debt is paid down.</summary>
public sealed record Baseline
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public List<string> Fingerprints { get; init; } = [];
}

public interface IBaselineService
{
    Task<Baseline?> LoadAsync(string path, CancellationToken cancellationToken = default);

    Task WriteAsync(
        string path,
        IEnumerable<Finding> findings,
        CancellationToken cancellationToken = default
    );

    IReadOnlyList<Finding> Apply(IReadOnlyList<Finding> findings, Baseline? baseline);
}

public sealed class BaselineService(IFileSystem fileSystem) : IBaselineService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<Baseline?> LoadAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        if (!fileSystem.FileExists(path))
        {
            return null;
        }

        try
        {
            var json = await fileSystem.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<Baseline>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task WriteAsync(
        string path,
        IEnumerable<Finding> findings,
        CancellationToken cancellationToken = default
    )
    {
        var baseline = new Baseline
        {
            Fingerprints = findings
                .Select(f => f.Fingerprint)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList(),
        };

        await fileSystem.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(baseline, JsonOptions),
            cancellationToken
        );
    }

    public IReadOnlyList<Finding> Apply(IReadOnlyList<Finding> findings, Baseline? baseline)
    {
        if (baseline is null || baseline.Fingerprints.Count == 0)
        {
            return findings;
        }

        var accepted = baseline.Fingerprints.ToHashSet(StringComparer.Ordinal);
        return findings.Where(f => !accepted.Contains(f.Fingerprint)).ToList();
    }
}
