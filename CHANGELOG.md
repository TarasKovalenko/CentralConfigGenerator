# Changelog

## [3.0.0] - 2026-11-13

### Breaking changes

- Targets `net10.0` and `net11.0`. `net8.0` and `net9.0` are no longer shipped, so the tool now needs the .NET 10 runtime or later.

### Changes

- NuGet.Configuration, NuGet.Protocol and NuGet.Versioning 6.13.2 -> 8.0.0.
- Spectre.Console and Spectre.Console.Cli 0.50.0 -> 0.57.2.
- Microsoft.Extensions.DependencyInjection 9.0.4 -> 11.0.0.
- System.Security.Cryptography.Pkcs pinned to 11.0.0 to avoid the vulnerable 8.0.1 pulled in by NuGet.Packaging (CVE-2026-70162, CVE-2026-84008).

## 2.0.0

A rewrite around one idea: migrating to Central Package Management should be provable, reversible, and only the beginning of keeping dependencies healthy.

### New commands

- `verify` — migrates, then diffs the resolved package graph before and after to prove NuGet still resolves identical packages. Rolls back automatically on drift (exit code `9`).
- `analyze` — 16 dependency-health rules with a 0–100 score, severity overrides, baselines, and `--fix` / `--fix-dry-run` auto-fixes.
- `update` — updates central versions, runs the tests, and rolls back on failure. `--bisect` keeps the largest subset that still passes and names what it held back.
- `revert` — undoes CPM, writing versions back into project files.
- `doctor` — environment diagnostics: SDK, solution layout, feed reachability, config, backups, `.gitignore`.
- `status` — workspace dashboard plus the single most useful next action.
- `tree` — resolved dependency graph as an ASCII tree.
- `batch` — runs the migration across every solution in a monorepo, optionally in parallel.
- `init` — scaffolds `.centralconfig.json` (JSON schema published under `schemas/`).
- `explain` — documents any analysis rule.
- `backups list | restore | prune` — inspect and roll back any change the tool made.
- `completions` — bash, zsh, fish and PowerShell completion scripts.

### Migration improvements

- **Format-preserving edits.** Encoding, BOM, line endings, comments, indentation and attribute order all survive; only what must change changes. `--encoding` and `--linewrap` override when you want normalisation instead.
- **Dry-run with unified diffs** (`--dry-run --diff`), planned entirely in memory so the preview and the write share one code path.
- **Backups before every write**, with restore, retention and pruning. Files the operation created are recorded too, so restoring removes them.
- `--merge` into an existing `Directory.Packages.props` without disturbing its comments or layout.
- Conflict strategies `Highest`, `Lowest`, `MostCommon`, `Fail`, plus `--interactive-conflicts`.
- `--transitive-pinning`, `--ignore-prerelease`, `--version-comparison`, `--keep-attrs`.
- F# and VB projects alongside C#; `.sln`, `.slnx` and `.slnf` scoping; `--exclude-dirs`.
- Versions declared as child elements (`<Version>1.0.0</Version>`) are handled, as are `GlobalPackageReference` and `VersionOverride`.

### Directory.Build.props

- Property hoisting now requires **unanimity**: a property moves up only when every project declaring it uses the same value. Conditioned `PropertyGroup`s and identity properties (`AssemblyName`, `RootNamespace`, …) are never touched.
- Configurable candidate list via `--property` or `buildProperties` in the config file.

### Reporting and CI

- Output as Terminal, JSON, SARIF 2.1.0, Markdown or CSV, to stdout or a file.
- Stable exit codes 0–9 so CI can branch on the outcome.
- `--fail-on`, `--rules Rule=Severity`, `--baseline` and `--write-baseline` for gradual adoption.

### Platform

- Multi-targets `net8.0`, `net9.0` and `net10.0`.
- Private feeds honoured through `nuget.config`.
- 320+ tests.

### Breaking changes

- `packages-enhanced` is now an alias for `migrate`; the enhanced analysis is the default path.
- The CLI moved from `System.CommandLine` to `Spectre.Console.Cli`. Command names are unchanged and `packages` / `convert` alias `migrate`.
- `-d` is `--directory`; use `-n` or `--dry-run` for dry runs.

## 1.1.1 and earlier

See the git history.
