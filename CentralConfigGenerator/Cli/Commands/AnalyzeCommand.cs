using System.ComponentModel;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Cli.Rendering;
using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Core.Reporting;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public sealed class AnalyzeSettings : WorkspaceSettings
{
    [CommandOption("-a|--audit")]
    [Description("Check every package against published security advisories.")]
    public bool Audit { get; init; }

    [CommandOption("--outdated")]
    [Description("Report packages that are behind the feed.")]
    public bool Outdated { get; init; }

    [CommandOption("--deprecated")]
    [Description("Report packages the author marked deprecated.")]
    public bool Deprecated { get; init; }

    [CommandOption("--licenses")]
    [Description("Flag copyleft, proprietary and undeclared licences.")]
    public bool Licenses { get; init; }

    [CommandOption("--transitive")]
    [Description("Resolve the transitive graph (requires the dotnet CLI).")]
    public bool Transitive { get; init; }

    [CommandOption("--all-checks")]
    [Description("Shorthand for --audit --outdated --deprecated --licenses --transitive.")]
    public bool AllChecks { get; init; }

    [CommandOption("--max-parallelism <N>")]
    [Description("How many packages to query concurrently. Default: 8.")]
    public int? MaxParallelism { get; init; }

    [CommandOption("--fail-on <SEVERITY>")]
    [Description("Exit non-zero at or above this severity: Info, Low, Moderate, High, Critical, Never.")]
    public string? FailOn { get; init; }

    [CommandOption("--rules <RULE=SEVERITY>")]
    [Description("Override a rule's severity, or disable it with Rule=none. Repeatable.")]
    public string[]? Rules { get; init; }

    [CommandOption("--baseline <PATH>")]
    [Description("Suppress findings recorded in this baseline file.")]
    public string? Baseline { get; init; }

    [CommandOption("--write-baseline")]
    [Description("Record the current findings as the baseline and exit successfully.")]
    public bool WriteBaseline { get; init; }

    [CommandOption("--ignore <PACKAGE>")]
    [Description("Ignore a package entirely. Supports a trailing wildcard. Repeatable.")]
    public string[]? Ignore { get; init; }

    [CommandOption("--include-prerelease")]
    [Description("Treat pre-release versions as acceptable upgrade targets.")]
    public bool IncludePrerelease { get; init; }

    [CommandOption("--output <FORMAT>")]
    [Description("Terminal (default), Json, Sarif, Markdown or Csv.")]
    public string? Output { get; init; }

    [CommandOption("--output-file <PATH>")]
    [Description("Write the structured report to a file instead of stdout.")]
    public string? OutputFile { get; init; }

    [CommandOption("--fix")]
    [Description("Apply every auto-fixable finding.")]
    public bool Fix { get; init; }

    [CommandOption("--fix-dry-run")]
    [Description("Show what --fix would change without writing.")]
    public bool FixDryRun { get; init; }

    [CommandOption("--no-backup")]
    [Description("Do not back up before applying fixes.")]
    public bool NoBackup { get; init; }

    [CommandOption("--backup-dir <PATH>")]
    [Description("Where fix backups are stored.")]
    public string? BackupDir { get; init; }
}

/// <summary>Runs the dependency health analysis and, optionally, its auto-fixes.</summary>
public sealed class AnalyzeCommand(
    IAnalysisEngine engine,
    IAutoFixService autoFixService,
    IBaselineService baselineService,
    EffectiveSettings effective
) : AsyncCommand<AnalyzeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeSettings settings, CancellationToken cancellationToken)
    {
        Ui.Quiet = settings.Quiet;
        Ui.Verbose = settings.Verbose;

        await effective.LoadAsync(settings);

        var format = ParseFormat(settings.Output);

        // Structured output goes to stdout, so progress chatter must not pollute it.
        if (format != ReportFormat.Terminal && settings.OutputFile is null)
        {
            Ui.Quiet = true;
        }

        var options = BuildOptions(settings);
        var report = await RunAsync(options, format);

        if (report.ProjectCount == 0)
        {
            Ui.Warn("No project files were found.");
            return ExitCode.NoProjectsFound;
        }

        if (settings.WriteBaseline)
        {
            var path = settings.Baseline ?? Path.Combine(options.RootDirectory, ".centralconfig-baseline.json");
            await baselineService.WriteAsync(path, report.Findings);
            Ui.Success($"Baseline written to {ReportRenderer.Relative(path)} ({report.Findings.Count} finding(s)).");
            return ExitCode.Success;
        }

        await EmitAsync(report, format, settings.OutputFile);

        if (settings.Fix || settings.FixDryRun)
        {
            var fixExit = await ApplyFixesAsync(options, report, settings);
            if (fixExit != ExitCode.Success)
            {
                return fixExit;
            }
        }

        if (report.IsIncomplete && report.Findings.Count == 0)
        {
            return ExitCode.IncompleteAnalysis;
        }

        var failOn = ParseSeverity(settings.FailOn) ?? effective.File?.FailOn ?? Severity.Info;

        if (failOn != Severity.Never && report.HighestSeverity >= failOn && report.Findings.Count > 0)
        {
            return ExitCode.AnalysisIssuesFound;
        }

        return ExitCode.Success;
    }

    private async Task<AnalysisReport> RunAsync(AnalysisOptions options, ReportFormat format)
    {
        if (Ui.Quiet || format != ReportFormat.Terminal)
        {
            return await engine.AnalyzeAsync(options);
        }

        return await Ui.StatusAsync(
            "Analysing dependencies…",
            async update => await engine.AnalyzeAsync(options, new Progress<string>(update))
        );
    }

    private async Task<int> ApplyFixesAsync(
        AnalysisOptions options,
        AnalysisReport report,
        AnalyzeSettings settings
    )
    {
        var fixable = report.Findings.Where(f => f.IsAutoFixable).ToList();

        if (fixable.Count == 0)
        {
            Ui.Info("[grey]Nothing to auto-fix.[/]");
            return ExitCode.Success;
        }

        var analysisContext = await engine.BuildContextAsync(options);
        var result = await autoFixService.PlanAsync(analysisContext, fixable);

        if (!result.HasChanges)
        {
            Ui.Info("[grey]The auto-fixable findings produced no file changes.[/]");
            return ExitCode.Success;
        }

        Ui.Blank();
        Ui.Rule("Auto-fix");

        foreach (var change in result.Changes)
        {
            Ui.Info($"[yellow]modify[/] {Markup.Escape(ReportRenderer.Relative(change.Path))}");

            if (settings.Verbose || settings.FixDryRun)
            {
                ReportRenderer.RenderDiff(change.Diff());
            }
        }

        if (settings.FixDryRun)
        {
            Ui.Info($"[grey]Dry run: {result.Fixed.Count} finding(s) would be fixed.[/]");
            return ExitCode.Success;
        }

        var backup = await autoFixService.ApplyAsync(
            result,
            options.RootDirectory,
            settings.BackupDir,
            createBackup: !settings.NoBackup
        );

        Ui.Success($"Fixed {result.Fixed.Count} finding(s) across {result.Changes.Count} file(s).");

        if (backup is not null)
        {
            Ui.Info($"[grey]Backup {backup.Id} created.[/]");
        }

        return ExitCode.Success;
    }

    private static async Task EmitAsync(
        AnalysisReport report,
        ReportFormat format,
        string? outputFile
    )
    {
        if (format == ReportFormat.Terminal)
        {
            ReportRenderer.RenderAnalysis(report);
            return;
        }

        var content = ReportWriterFactory.Create(format).Write(report);

        if (outputFile is null)
        {
            Console.Out.Write(content);
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputFile));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputFile, content);
        Ui.Success($"Report written to {ReportRenderer.Relative(outputFile)}.");
    }

    private AnalysisOptions BuildOptions(AnalyzeSettings settings)
    {
        var all = settings.AllChecks;

        return new AnalysisOptions
        {
            RootDirectory = settings.ResolvedDirectory,
            SolutionPath = settings.Solution,
            ProjectPath = settings.Project,
            ExcludePattern = effective.Text(settings.ExcludeDirs, c => c.ExcludeDirs),
            Audit = all || settings.Audit,
            Outdated = all || settings.Outdated,
            Deprecated = all || settings.Deprecated,
            Licenses = all || settings.Licenses,
            Transitive = all || settings.Transitive,
            MaxParallelism = settings.MaxParallelism ?? 8,
            FailOn = ParseSeverity(settings.FailOn) ?? effective.File?.FailOn ?? Severity.Info,
            RuleOverrides = effective.RuleOverrides(settings.Rules),
            BaselinePath = settings.WriteBaseline
                ? null
                : effective.Text(settings.Baseline, c => c.Baseline),
            IgnorePackages = (settings.Ignore ?? []).Concat(effective.File?.IgnorePackages ?? []).ToList(),
            IncludePrerelease = settings.IncludePrerelease,
        };
    }

    private static ReportFormat ParseFormat(string? value) =>
        Enum.TryParse<ReportFormat>(value, true, out var parsed) ? parsed : ReportFormat.Terminal;

    private static Severity? ParseSeverity(string? value) =>
        Enum.TryParse<Severity>(value, true, out var parsed) ? parsed : null;
}
