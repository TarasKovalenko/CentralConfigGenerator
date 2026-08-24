using System.ComponentModel;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Cli.Rendering;
using CentralConfigGenerator.Core.Migration;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Services;
using NuGet.Versioning;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public class MigrateSettings : MutatingSettings
{
    [CommandOption("-o|--output-dir <PATH>")]
    [Description("Where Directory.Packages.props is written. Defaults to the scanned directory.")]
    public string? OutputDir { get; init; }

    [CommandOption("-m|--merge")]
    [Description("Merge into an existing Directory.Packages.props instead of replacing it.")]
    public bool Merge { get; init; }

    [CommandOption("--overwrite")]
    [Description("Replace an existing Directory.Packages.props.")]
    public bool Overwrite { get; init; }

    [CommandOption("-k|--keep-attrs")]
    [Description("Leave inline Version attributes in the project files.")]
    public bool KeepAttributes { get; init; }

    [CommandOption("--conflict-strategy <STRATEGY>")]
    [Description("Highest (default), Lowest, MostCommon or Fail.")]
    public string? ConflictStrategy { get; init; }

    [CommandOption("--interactive-conflicts")]
    [Description("Prompt for every version conflict instead of resolving automatically.")]
    public bool InteractiveConflicts { get; init; }

    [CommandOption("--min-version")]
    [Description("Shorthand for --conflict-strategy Lowest.")]
    public bool MinVersion { get; init; }

    [CommandOption("--ignore-prerelease")]
    [Description("Prefer stable versions when resolving conflicts.")]
    public bool IgnorePrerelease { get; init; }

    [CommandOption("-t|--transitive-pinning")]
    [Description("Enable CentralPackageTransitivePinningEnabled in the generated file.")]
    public bool TransitivePinning { get; init; }

    [CommandOption("-e|--encoding <NAME>")]
    [Description("Write files with this encoding (IANA name, e.g. utf-8). Default: preserve.")]
    public string? Encoding { get; init; }

    [CommandOption("-l|--linewrap <STYLE>")]
    [Description("Line endings to write: lf, crlf or cr. Default: preserve.")]
    public string? LineWrap { get; init; }

    [CommandOption("-c|--version-comparison <SCOPE>")]
    [Description("Version comparison scope: Default, Version, VersionRelease or VersionReleaseMetadata.")]
    public string? VersionComparison { get; init; }

    [CommandOption("--unify-props")]
    [Description("Also promote shared MSBuild properties into Directory.Build.props.")]
    public bool UnifyProps { get; init; }

    [CommandOption("--include-fsharp")]
    [Description("Include .fsproj and .vbproj files (on by default).")]
    public bool IncludeFSharp { get; init; } = true;
}

/// <summary>Migrates a workspace to central package management.</summary>
public class MigrateCommand(
    IMigrationService migrationService,
    EffectiveSettings effective
) : AsyncCommand<MigrateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, MigrateSettings settings)
    {
        Ui.Quiet = settings.Quiet;
        Ui.Verbose = settings.Verbose;

        await effective.LoadAsync(settings);

        var options = BuildOptions(settings);

        if (
            !settings.Merge
            && !settings.Overwrite
            && !settings.DryRun
            && File.Exists(options.PackagesPropsPath)
        )
        {
            Ui.Warn(
                $"{Path.GetFileName(options.PackagesPropsPath)} already exists. "
                    + "Use --merge to combine, or --overwrite to replace it."
            );
            return ExitCode.ValidationError;
        }

        MigrationPlan plan;

        try
        {
            // Interactive conflict prompts cannot run inside a spinner, so skip it in that mode.
            var interactive = settings.InteractiveConflicts && Ui.IsInteractive;

            plan = interactive
                ? await migrationService.PlanAsync(options, PromptForVersion)
                : await Ui.StatusAsync(
                    "Analysing projects…",
                    async _ => await migrationService.PlanAsync(options)
                );
        }
        catch (VersionConflictException ex)
        {
            Ui.Error(ex.Message);
            return ExitCode.VersionConflict;
        }

        if (plan.ProjectCount == 0)
        {
            Ui.Warn("No project files were found. Check --directory, --solution or --exclude-dirs.");
            return ExitCode.NoProjectsFound;
        }

        Ui.Info(
            $"Found [bold]{plan.ProjectCount}[/] project(s) and [bold]{plan.PackageCount}[/] distinct package(s)."
        );
        Ui.Blank();

        ReportRenderer.RenderConflicts(plan.Conflicts);

        if (!plan.HasChanges)
        {
            Ui.Success("Nothing to change — this workspace is already centralised.");
            return ExitCode.Success;
        }

        ReportRenderer.RenderPlan(plan, settings.Diff, options.RootDirectory);

        if (settings.DryRun)
        {
            Ui.Blank();
            Ui.Info("[grey]Dry run: no files were written.[/]");
            return ExitCode.Success;
        }

        if (
            !settings.Force
            && plan.Conflicts.Count > 0
            && !Ui.Confirm("Version conflicts were resolved automatically. Continue?")
        )
        {
            Ui.Warn("Cancelled.");
            return ExitCode.Success;
        }

        var backup = await migrationService.ApplyAsync(plan, options);

        Ui.Blank();
        Ui.Success($"Wrote {plan.Changes.Count} file(s).");

        if (backup is not null)
        {
            Ui.Info(
                $"[grey]Backup {backup.Id} created — roll back with 'central-config backups restore'.[/]"
            );
        }

        return ExitCode.Success;
    }

    private MigrationOptions BuildOptions(MigrateSettings settings)
    {
        var strategy = settings.MinVersion
            ? VersionResolutionStrategy.Lowest
            : effective.Strategy(settings.ConflictStrategy);

        var kinds = new List<ProjectKind> { ProjectKind.CSharp };
        if (settings.IncludeFSharp)
        {
            kinds.Add(ProjectKind.FSharp);
            kinds.Add(ProjectKind.VisualBasic);
        }

        return new MigrationOptions
        {
            RootDirectory = settings.ResolvedDirectory,
            SolutionPath = settings.Solution,
            ProjectPath = settings.Project,
            OutputDirectory = settings.OutputDir,
            ExcludePattern = effective.Text(settings.ExcludeDirs, c => c.ExcludeDirs),
            Merge = settings.Merge,
            KeepInlineVersions = settings.KeepAttributes,
            ConflictStrategy = strategy,
            FailOnConflict = strategy == VersionResolutionStrategy.Fail,
            IgnorePrerelease = effective.Bool(settings.IgnorePrerelease, c => c.IgnorePrerelease),
            TransitivePinning = effective.Bool(
                settings.TransitivePinning,
                c => c.TransitivePinning
            ),
            VersionComparison = ParseComparison(settings.VersionComparison),
            LineEnding = ParseLineEnding(effective.Text(settings.LineWrap, c => c.LineEnding?.ToString())),
            EncodingName = effective.Text(settings.Encoding, c => c.Encoding),
            CreateBackup = !settings.NoBackup && effective.File?.Backup != false,
            BackupDirectory = effective.Text(settings.BackupDir, c => c.BackupDir),
            AddGitIgnore = effective.Bool(settings.AddGitIgnore, c => c.AddGitignore),
            UnifyProperties = settings.UnifyProps,
            BuildProperties = effective.File?.BuildProperties ?? BuildPropertyDefaults.Recommended,
            IncludedKinds = kinds,
        };
    }

    private static string PromptForVersion(PackageConflict conflict)
    {
        var versions = conflict.DistinctVersions.ToList();

        AnsiConsole.MarkupLine(
            $"[yellow]Conflict:[/] [bold]{Markup.Escape(conflict.PackageId)}[/] is referenced by "
                + $"{conflict.References.Count} project(s)."
        );

        foreach (var reference in conflict.References.OrderBy(r => r.ProjectPath))
        {
            AnsiConsole.MarkupLine(
                $"  [grey]{Markup.Escape(Path.GetFileName(reference.ProjectPath))}[/] → {Markup.Escape(reference.Version ?? "-")}"
            );
        }

        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"Which version should [bold]{Markup.Escape(conflict.PackageId)}[/] use?")
                .AddChoices(versions)
        );
    }

    private static VersionComparison ParseComparison(string? value) =>
        Enum.TryParse<VersionComparison>(value, true, out var parsed)
            ? parsed
            : VersionComparison.VersionRelease;

    private static LineEndingStyle ParseLineEnding(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "lf" => LineEndingStyle.Lf,
            "crlf" => LineEndingStyle.CrLf,
            "cr" => LineEndingStyle.Cr,
            _ => Enum.TryParse<LineEndingStyle>(value, true, out var parsed)
                ? parsed
                : LineEndingStyle.Preserve,
        };
}
