using CentralConfigGenerator.Core.Configuration;
using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.IO;
using CentralConfigGenerator.Core.NuGet;
using CentralConfigGenerator.Core.Services;
using CentralConfigGenerator.Core.Xml;

namespace CentralConfigGenerator.Core.Workspace;

public enum CheckStatus
{
    Pass,
    Warn,
    Fail,
    Skipped,
}

public sealed record DiagnosticCheck
{
    public required string Name { get; init; }

    public required CheckStatus Status { get; init; }

    public required string Detail { get; init; }

    public string? Remedy { get; init; }
}

public sealed record DoctorReport
{
    public required IReadOnlyList<DiagnosticCheck> Checks { get; init; }

    public bool HasFailures => Checks.Any(c => c.Status == CheckStatus.Fail);

    public bool HasWarnings => Checks.Any(c => c.Status == CheckStatus.Warn);
}

/// <summary>
/// Environment diagnostics. Answers "why did that not work?" before the user has to ask, by
/// checking the SDK, the workspace layout and feed reachability in one pass.
/// </summary>
public sealed class DoctorService(
    IFileSystem fileSystem,
    IDotNetCliService cli,
    IProjectDiscoveryService discovery,
    INuGetFeedService feedService,
    ConfigurationLoader configurationLoader
)
{
    public async Task<DoctorReport> RunAsync(
        string rootDirectory,
        CancellationToken cancellationToken = default
    )
    {
        var checks = new List<DiagnosticCheck>();
        var root = Path.GetFullPath(rootDirectory);

        checks.Add(await CheckSdkAsync(root, cancellationToken));
        checks.Add(CheckGlobalJson(root));
        checks.Add(CheckSolutions(root));

        var projects = await discovery.DiscoverAsync(
            new DiscoveryOptions { RootDirectory = root },
            cancellationToken
        );

        checks.Add(
            new DiagnosticCheck
            {
                Name = "Projects",
                Status = projects.Count > 0 ? CheckStatus.Pass : CheckStatus.Fail,
                Detail = projects.Count > 0
                    ? $"{projects.Count} project file(s) discovered."
                    : "No .csproj/.fsproj/.vbproj found under this directory.",
                Remedy = projects.Count > 0 ? null : "Run from the repository root, or pass --directory.",
            }
        );

        checks.Add(await CheckCentralPackageManagementAsync(root, cancellationToken));
        checks.Add(CheckBuildProps(root));
        checks.Add(await CheckFeedsAsync(cancellationToken));
        checks.Add(await CheckConfigurationAsync(root, cancellationToken));
        checks.Add(CheckBackups(root));
        checks.Add(CheckGitIgnore(root));

        return new DoctorReport { Checks = checks };
    }

    private async Task<DiagnosticCheck> CheckSdkAsync(
        string root,
        CancellationToken cancellationToken
    )
    {
        if (!cli.IsAvailable)
        {
            return new DiagnosticCheck
            {
                Name = ".NET SDK",
                Status = CheckStatus.Fail,
                Detail = "The dotnet CLI was not found on PATH.",
                Remedy = "Install the .NET SDK 8.0 or later from https://dot.net.",
            };
        }

        var result = await cli.RunAsync(
            "--version",
            root,
            TimeSpan.FromSeconds(30),
            cancellationToken
        );

        var version = result.StandardOutput.Trim();

        if (!result.Succeeded)
        {
            return new DiagnosticCheck
            {
                Name = ".NET SDK",
                Status = CheckStatus.Fail,
                Detail = result.CombinedOutput.Trim(),
                Remedy = "Check global.json — it may pin an SDK version that is not installed.",
            };
        }

        var major = ParseMajor(version);

        return new DiagnosticCheck
        {
            Name = ".NET SDK",
            Status = major >= 8 ? CheckStatus.Pass : CheckStatus.Warn,
            Detail = $"dotnet {version}",
            Remedy = major >= 8
                ? null
                : "Central package management needs SDK 6.0+; transitive pinning needs 8.0+.",
        };
    }

    private DiagnosticCheck CheckGlobalJson(string root)
    {
        var path = Path.Combine(root, "global.json");

        return fileSystem.FileExists(path)
            ? new DiagnosticCheck
            {
                Name = "global.json",
                Status = CheckStatus.Pass,
                Detail = "Present — the SDK version is pinned for this repository.",
            }
            : new DiagnosticCheck
            {
                Name = "global.json",
                Status = CheckStatus.Warn,
                Detail = "Not present.",
                Remedy = "Add a global.json so every machine builds with the same SDK.",
            };
    }

    private DiagnosticCheck CheckSolutions(string root)
    {
        var solutions = fileSystem
            .EnumerateFiles(root, "*", recursive: false)
            .Where(SolutionReader.IsSolution)
            .ToList();

        return solutions.Count switch
        {
            0 => new DiagnosticCheck
            {
                Name = "Solution",
                Status = CheckStatus.Warn,
                Detail = "No solution file in this directory; the scan will walk the whole tree.",
                Remedy = "Pass --solution to scope the run precisely.",
            },
            1 => new DiagnosticCheck
            {
                Name = "Solution",
                Status = CheckStatus.Pass,
                Detail = Path.GetFileName(solutions[0]),
            },
            _ => new DiagnosticCheck
            {
                Name = "Solution",
                Status = CheckStatus.Warn,
                Detail = $"{solutions.Count} solutions found: {string.Join(", ", solutions.Select(Path.GetFileName))}.",
                Remedy = "Pass --solution so the tool knows which one you mean.",
            },
        };
    }

    private async Task<DiagnosticCheck> CheckCentralPackageManagementAsync(
        string root,
        CancellationToken cancellationToken
    )
    {
        var path = Path.Combine(root, "Directory.Packages.props");

        if (!fileSystem.FileExists(path))
        {
            return new DiagnosticCheck
            {
                Name = "Central package management",
                Status = CheckStatus.Warn,
                Detail = "Directory.Packages.props not found.",
                Remedy = "Run 'central-config migrate' to create it.",
            };
        }

        var file = await discovery.LoadAsync(path, cancellationToken);

        if (file is null)
        {
            return new DiagnosticCheck
            {
                Name = "Central package management",
                Status = CheckStatus.Fail,
                Detail = "Directory.Packages.props exists but could not be read.",
                Remedy = "Check file permissions.",
            };
        }

        if (!MsBuildXml.TryParse(file.Content, out var document, out var error))
        {
            return new DiagnosticCheck
            {
                Name = "Central package management",
                Status = CheckStatus.Fail,
                Detail = $"Directory.Packages.props could not be parsed: {error}",
                Remedy = "Fix the XML, or restore it from .centralconfig-backups.",
            };
        }

        var enabled = MsBuildXml
            .Descendants(document, "ManagePackageVersionsCentrally")
            .Any(e => string.Equals(e.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));

        var count = PackageReferenceReader.ReadPackageVersions(document).Count;

        return new DiagnosticCheck
        {
            Name = "Central package management",
            Status = enabled ? CheckStatus.Pass : CheckStatus.Warn,
            Detail = enabled
                ? $"Enabled with {count} pinned package(s)."
                : $"{count} entries present but ManagePackageVersionsCentrally is not true.",
            Remedy = enabled
                ? null
                : "Set <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>.",
        };
    }

    private DiagnosticCheck CheckBuildProps(string root)
    {
        var path = Path.Combine(root, "Directory.Build.props");

        return fileSystem.FileExists(path)
            ? new DiagnosticCheck
            {
                Name = "Directory.Build.props",
                Status = CheckStatus.Pass,
                Detail = "Present — shared properties are centralised.",
            }
            : new DiagnosticCheck
            {
                Name = "Directory.Build.props",
                Status = CheckStatus.Warn,
                Detail = "Not present.",
                Remedy = "Run 'central-config build' to hoist shared properties.",
            };
    }

    private async Task<DiagnosticCheck> CheckFeedsAsync(CancellationToken cancellationToken)
    {
        var sources = feedService.Sources;

        if (sources.Count == 0)
        {
            return new DiagnosticCheck
            {
                Name = "NuGet feeds",
                Status = CheckStatus.Fail,
                Detail = "No enabled package sources were found.",
                Remedy = "Check your nuget.config.",
            };
        }

        // A cheap round-trip against a package that exists on every public mirror.
        var probe = await feedService.GetMetadataAsync("Newtonsoft.Json", cancellationToken);

        return probe.Found
            ? new DiagnosticCheck
            {
                Name = "NuGet feeds",
                Status = CheckStatus.Pass,
                Detail = $"{sources.Count} source(s) configured and reachable.",
            }
            : new DiagnosticCheck
            {
                Name = "NuGet feeds",
                Status = CheckStatus.Warn,
                Detail = probe.Error ?? "The probe package could not be resolved.",
                Remedy = "Online checks (--audit, --outdated) will be skipped or incomplete.",
            };
    }

    private async Task<DiagnosticCheck> CheckConfigurationAsync(
        string root,
        CancellationToken cancellationToken
    )
    {
        var (settings, path, error) = await configurationLoader.LoadAsync(root, cancellationToken);

        if (error is not null)
        {
            return new DiagnosticCheck
            {
                Name = "Configuration",
                Status = CheckStatus.Fail,
                Detail = $"{path} is invalid: {error}",
                Remedy = "Fix the JSON or delete the file.",
            };
        }

        return settings is null
            ? new DiagnosticCheck
            {
                Name = "Configuration",
                Status = CheckStatus.Skipped,
                Detail = $"No {ConfigurationLoader.FileName} found; command-line defaults apply.",
                Remedy = "Run 'central-config init' to scaffold one.",
            }
            : new DiagnosticCheck
            {
                Name = "Configuration",
                Status = CheckStatus.Pass,
                Detail = $"Loaded from {path}.",
            };
    }

    private DiagnosticCheck CheckBackups(string root)
    {
        var path = Path.Combine(root, BackupService.DefaultBackupFolderName);

        if (!fileSystem.DirectoryExists(path))
        {
            return new DiagnosticCheck
            {
                Name = "Backups",
                Status = CheckStatus.Skipped,
                Detail = "No backups yet.",
            };
        }

        var count = fileSystem.EnumerateDirectories(path).Count;

        return new DiagnosticCheck
        {
            Name = "Backups",
            Status = count > 10 ? CheckStatus.Warn : CheckStatus.Pass,
            Detail = $"{count} backup set(s) stored.",
            Remedy = count > 10 ? "Run 'central-config backups prune' to trim old sets." : null,
        };
    }

    private DiagnosticCheck CheckGitIgnore(string root)
    {
        var gitDirectory = Path.Combine(root, ".git");
        if (!fileSystem.DirectoryExists(gitDirectory))
        {
            return new DiagnosticCheck
            {
                Name = ".gitignore",
                Status = CheckStatus.Skipped,
                Detail = "Not a git repository.",
            };
        }

        var path = Path.Combine(root, ".gitignore");
        if (!fileSystem.FileExists(path))
        {
            return new DiagnosticCheck
            {
                Name = ".gitignore",
                Status = CheckStatus.Warn,
                Detail = "No .gitignore in the repository root.",
                Remedy = "Pass --add-gitignore so backups are not committed.",
            };
        }

        var content = fileSystem.ReadAllTextAsync(path).GetAwaiter().GetResult();
        var ignored = content.Contains(
            BackupService.DefaultBackupFolderName,
            StringComparison.OrdinalIgnoreCase
        );

        return new DiagnosticCheck
        {
            Name = ".gitignore",
            Status = ignored ? CheckStatus.Pass : CheckStatus.Warn,
            Detail = ignored
                ? "Backup directory is ignored."
                : "Backup directory is not ignored.",
            Remedy = ignored ? null : "Pass --add-gitignore on the next run.",
        };
    }

    private static int ParseMajor(string version)
    {
        var head = version.Split('.').FirstOrDefault();
        return int.TryParse(head, out var major) ? major : 0;
    }
}
