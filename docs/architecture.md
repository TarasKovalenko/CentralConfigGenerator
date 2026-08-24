# Architecture

Two assemblies. `CentralConfigGenerator.Core` holds everything that does real work and has no console dependency in its logic; `CentralConfigGenerator` is the Spectre.Console.Cli front end.

## Core

| Namespace | Responsibility |
|---|---|
| `IO` | `IFileSystem` seam, plus `EncodingDetector` — decodes bytes into text while recording encoding, BOM and line-ending style so a file can be rewritten byte-faithfully. |
| `Discovery` | `ProjectDiscoveryService` walks a directory or a solution (`.sln`, `.slnx`, `.slnf`) and loads project files with their original format attached. |
| `Xml` | `MsBuildXml` parses with `LoadOptions.PreserveWhitespace` and offers namespace-agnostic, case-insensitive lookups; `PackageReferenceReader` extracts references including child-element versions, `VersionOverride` and `GlobalPackageReference`. |
| `Migration` | `MigrationService` plans changes in memory (`MigrationPlan` → `FileChange`), `ProjectRewriter` performs surgical edits, `PropertyHoister` decides what is safe to lift into `Directory.Build.props`. |
| `Generators` | `PackagesPropsWriter` / `BuildPropsWriter` create files from scratch or merge into existing ones; `PackagesPropsEditor` makes targeted single-entry edits. |
| `Analysis` | `AnalysisEngine` builds one `AnalysisContext` and runs every enabled `IAnalysisRule` against it; `AutoFixService` turns findings into batched file edits; `BaselineService` suppresses accepted findings. |
| `NuGet` | `NuGetFeedService` queries every configured source (nuget.config aware) for versions, deprecation, licence and advisories; `PackageGraphService` parses `dotnet list package --format json`. |
| `Workspace` | `VerificationService` (graph diffing), `PackageUpdateService` (update + test + bisect), `DoctorService`, `StatusService`, `DependencyTreeService`, `BatchService`. |
| `Reporting` | `JsonReportWriter`, `SarifReportWriter`, `MarkdownReportWriter`, `CsvReportWriter`. |
| `Configuration` | `.centralconfig.json` discovery and scaffolding. |

## Two invariants worth knowing

**Planning never writes.** `PlanAsync` returns a `MigrationPlan` holding, for each file, its original and new content. Dry-run prints it, `--diff` diffs it, `ApplyAsync` writes it. There is no second code path that could disagree with the preview.

**Format is data.** Every `ProjectFile` carries a `TextFileFormat` describing how it was decoded. Writing re-encodes through the same record, so a CRLF UTF-8-BOM file stays a CRLF UTF-8-BOM file unless `--encoding` or `--linewrap` says otherwise.

## Adding an analysis rule

1. Implement `IAnalysisRule` in `Analysis/Rules`. `Description` and `Rationale` are user-facing — they are what `explain` prints and what lands in SARIF.
2. Add its id to `RuleIds`.
3. Register it in `RuleCatalog.CreateAll()`.
4. If it should only run with a flag, override `IsEnabled(AnalysisOptions)`. If it needs feed data, override `RequiresNetwork`. If it only makes sense under CPM, override `RequiresCentralPackageManagement`.
5. Give the finding a `Fix`/`FixValue` only if `AutoFixService` can genuinely carry it out — claiming a fix that never applies is worse than reporting none.

## Adding a command

1. A settings record deriving from `WorkspaceSettings` or `MutatingSettings`.
2. An `AsyncCommand<TSettings>` in `Cli/Commands`.
3. Register it in `Program.BuildServiceCollection()` and `app.Configure`.
4. Return a constant from `ExitCode` — scripts depend on those numbers.
