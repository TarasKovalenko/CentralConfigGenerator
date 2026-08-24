using System.Text.Json;
using System.Text.Json.Serialization;
using CentralConfigGenerator.Core.IO;

namespace CentralConfigGenerator.Core.Configuration;

/// <summary>
/// Discovers and reads <c>.centralconfig.json</c> by walking up from a starting directory,
/// mirroring how <c>.editorconfig</c> and <c>global.json</c> are resolved.
/// </summary>
public sealed class ConfigurationLoader(IFileSystem fileSystem)
{
    public const string FileName = ".centralconfig.json";

    public const string SchemaUrl =
        "https://raw.githubusercontent.com/TarasKovalenko/CentralConfigGenerator/main/schemas/centralconfig.schema.json";

    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public string? Find(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, FileName);
            if (fileSystem.FileExists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    public async Task<(CentralConfigSettings? Settings, string? Path, string? Error)> LoadAsync(
        string startDirectory,
        CancellationToken cancellationToken = default
    )
    {
        var path = Find(startDirectory);
        if (path is null)
        {
            return (null, null, null);
        }

        try
        {
            var json = await fileSystem.ReadAllTextAsync(path, cancellationToken);
            var settings = JsonSerializer.Deserialize<CentralConfigSettings>(
                json,
                SerializerOptions
            );
            return (settings, path, null);
        }
        catch (JsonException ex)
        {
            return (null, path, ex.Message);
        }
    }

    public async Task<string> ScaffoldAsync(
        string directory,
        bool overwrite,
        CancellationToken cancellationToken = default
    )
    {
        var path = Path.Combine(Path.GetFullPath(directory), FileName);

        if (fileSystem.FileExists(path) && !overwrite)
        {
            return path;
        }

        var template = new CentralConfigSettings
        {
            Schema = SchemaUrl,
            ConflictStrategy = VersionResolutionStrategyName.Highest,
            Backup = true,
            AddGitignore = true,
            FailOn = Analysis.Severity.High,
            TransitivePinning = false,
            IgnorePrerelease = false,
            Retention = new RetentionSettings { Enabled = true, MaxBackups = 5 },
            Rules = new Dictionary<string, string>
            {
                [Analysis.RuleIds.OutdatedPackage] = "Low",
                [Analysis.RuleIds.LicenseRisk] = "Moderate",
            },
            BuildProperties =
            [
                "TargetFramework",
                "TargetFrameworks",
                "ImplicitUsings",
                "Nullable",
                "LangVersion",
            ],
        };

        await fileSystem.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(template, SerializerOptions),
            cancellationToken
        );

        return path;
    }
}
