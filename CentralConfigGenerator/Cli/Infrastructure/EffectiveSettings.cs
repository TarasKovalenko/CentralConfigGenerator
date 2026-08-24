using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Configuration;
using CentralConfigGenerator.Core.Services;

namespace CentralConfigGenerator.Cli.Infrastructure;

/// <summary>
/// Loads <c>.centralconfig.json</c> and folds it together with the command line. Explicit
/// command-line values always win; the file only fills in what was not specified.
/// </summary>
public sealed class EffectiveSettings(ConfigurationLoader loader)
{
    public CentralConfigSettings? File { get; private set; }

    public string? FilePath { get; private set; }

    public async Task LoadAsync(
        WorkspaceSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        if (settings.NoConfig)
        {
            return;
        }

        var (file, path, error) = await loader.LoadAsync(
            settings.ResolvedDirectory,
            cancellationToken
        );

        if (error is not null)
        {
            Ui.Warn($"Ignoring {path}: {error}");
            return;
        }

        File = file;
        FilePath = path;

        if (file is not null)
        {
            Ui.Detail($"Configuration loaded from {path}");
        }
    }

    public bool Bool(bool commandLineValue, Func<CentralConfigSettings, bool?> selector) =>
        commandLineValue || (File is not null && selector(File) == true);

    public T Value<T>(T? commandLineValue, Func<CentralConfigSettings, T?> selector, T fallback)
        where T : struct =>
        commandLineValue ?? (File is not null ? selector(File) : null) ?? fallback;

    public string? Text(string? commandLineValue, Func<CentralConfigSettings, string?> selector) =>
        commandLineValue ?? (File is not null ? selector(File) : null);

    public VersionResolutionStrategy Strategy(string? commandLineValue)
    {
        if (
            commandLineValue is not null
            && Enum.TryParse<VersionResolutionStrategy>(commandLineValue, true, out var parsed)
        )
        {
            return parsed;
        }

        return File?.ConflictStrategy switch
        {
            VersionResolutionStrategyName.Lowest => VersionResolutionStrategy.Lowest,
            VersionResolutionStrategyName.MostCommon => VersionResolutionStrategy.MostCommon,
            VersionResolutionStrategyName.Fail => VersionResolutionStrategy.Fail,
            VersionResolutionStrategyName.Manual => VersionResolutionStrategy.Manual,
            _ => VersionResolutionStrategy.Highest,
        };
    }

    /// <summary>Merges per-rule severity overrides from the file and the <c>--rules</c> flag.</summary>
    public IReadOnlyDictionary<string, Severity?> RuleOverrides(string[]? commandLineRules)
    {
        var result = new Dictionary<string, Severity?>(StringComparer.OrdinalIgnoreCase);

        if (File?.Rules is not null)
        {
            foreach (var (ruleId, value) in File.Rules)
            {
                Add(result, ruleId, value);
            }
        }

        foreach (var entry in commandLineRules ?? [])
        {
            var parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                Add(result, parts[0], parts[1]);
            }
        }

        return result;
    }

    private static void Add(Dictionary<string, Severity?> target, string ruleId, string value)
    {
        if (RuleCatalog.Find(ruleId) is null)
        {
            Ui.Warn($"Unknown rule '{ruleId}' — ignoring. Run 'central-config explain all' to list rules.");
            return;
        }

        if (
            string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "disabled", StringComparison.OrdinalIgnoreCase)
        )
        {
            target[ruleId] = null;
            return;
        }

        if (Enum.TryParse<Severity>(value, true, out var severity))
        {
            target[ruleId] = severity;
        }
        else
        {
            Ui.Warn($"Unknown severity '{value}' for rule '{ruleId}' — ignoring.");
        }
    }
}
