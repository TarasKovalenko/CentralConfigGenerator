# CentralConfigGenerator

[![Made in Ukraine](https://img.shields.io/badge/made_in-ukraine-ffd700.svg?labelColor=0057b7)](https://taraskovalenko.github.io/)
[![build](https://github.com/TarasKovalenko/CentralConfigGenerator/actions/workflows/dotnet.yml/badge.svg)](https://github.com/TarasKovalenko/CentralConfigGenerator/actions)
[![CentralConfigGenerator NuGet current](https://img.shields.io/nuget/v/CentralConfigGenerator?label=CentralConfigGenerator)](https://www.nuget.org/packages/CentralConfigGenerator/)

Migrate a .NET solution to Central Package Management, **prove the migration changed nothing**, and keep the dependencies healthy afterwards.

Writing `Directory.Packages.props` is step one. The tool shows you the exact diff before touching a file, backs everything up so you can undo it, verifies that NuGet still resolves the identical package graph afterwards, audits every dependency for vulnerabilities, deprecations and licence risk, fixes what can be fixed automatically, and moves versions forward with your test suite as the safety net.

## Terms of use

By using this project or its source code, for any purpose and in any shape or form, you grant your **implicit agreement** to all of the following statements:

- You unequivocally condemn Russia and its military aggression against Ukraine
- You recognize that Russia is an occupant that unlawfully invaded a sovereign state
- You agree that [Russia is a terrorist state](https://www.europarl.europa.eu/doceo/document/RC-9-2022-0482_EN.html)
- You fully support Ukraine's territorial integrity, including its claims over [temporarily occupied territories](https://en.wikipedia.org/wiki/Russian-occupied_territories_of_Ukraine)
- You reject false narratives perpetuated by Russian state propaganda

To learn more about the war and how you can help, [click here](https://war.ukraine.ua/). Glory to Ukraine! 🇺🇦

---

## Install

```bash
dotnet tool install --global CentralConfigGenerator
```

Requires the .NET SDK 8.0 or later. The tool ships for `net8.0`, `net9.0` and `net10.0`.

```bash
dotnet tool update --global CentralConfigGenerator   # upgrade
central-config --version                             # verify
```

## 60 seconds to a migrated solution

```bash
central-config doctor                    # is this machine and repo ready?
central-config migrate --dry-run --diff  # exactly what would change, line by line
central-config verify                    # migrate, then prove the package graph is identical
central-config analyze --all-checks      # what else is wrong with these dependencies?
central-config analyze --fix             # fix everything that can be fixed safely
```

Nothing is written without a backup. `central-config backups restore` undoes any of it.

---

## Commands

| Command | What it does |
|---|---|
| `migrate` | Move the workspace to Central Package Management. Aliases: `packages`, `convert`, `packages-enhanced`. |
| `build` | Hoist MSBuild properties every project agrees on into `Directory.Build.props`. |
| `all` | `migrate` plus `build` in one pass. |
| `revert` | Undo CPM: write versions back into the project files and delete `Directory.Packages.props`. |
| `analyze` | Score dependency health, report findings, optionally auto-fix. Alias: `check`. |
| `update` | Update central versions, run the tests, roll back or bisect on failure. |
| `verify` | Migrate, then diff the resolved package graph before and after. |
| `tree` | Print the resolved dependency graph as an ASCII tree. |
| `status` | One-shot dashboard of the workspace and the single most useful next step. |
| `doctor` | Environment diagnostics: SDK, solution layout, feeds, config, backups. |
| `init` | Scaffold a `.centralconfig.json`. |
| `explain` | Document an analysis rule, or list all of them. |
| `batch` | Run the migration across every solution in a monorepo. |
| `backups list \| restore \| prune` | Inspect, restore and prune rollback backups. |
| `completions` | Emit a shell completion script (bash, zsh, fish, powershell). |

Every command accepts `--help`.

### Scoping the run

These work on every command that touches a workspace:

| Option | Meaning |
|---|---|
| `-d, --directory <PATH>` | Directory to scan. Defaults to the current directory. |
| `-s, --solution <PATH>` | Scope to a `.sln`, `.slnx` or `.slnf`. Only its projects are touched. |
| `--project <PATH>` | Scope to a single project. |
| `-x, --exclude-dirs <REGEX>` | Skip matching directories. `bin`, `obj`, `node_modules`, `.git` and the backup folder are always skipped. |
| `-q, --quiet` / `-v, --verbose` | Less / more output. |
| `--no-config` | Ignore any `.centralconfig.json` on disk. |

C#, F# and VB projects are all discovered (`.csproj`, `.fsproj`, `.vbproj`).

---

## `migrate`

```bash
central-config migrate [options]
```

| Option | Meaning |
|---|---|
| `-n, --dry-run` | Plan everything, write nothing. |
| `--diff` | Print a unified diff of every pending change. |
| `-o, --output-dir <PATH>` | Where `Directory.Packages.props` is written. |
| `-m, --merge` | Merge into an existing props file, preserving its comments and layout. |
| `--overwrite` | Replace an existing props file. |
| `-k, --keep-attrs` | Leave inline `Version` attributes in the projects. |
| `--conflict-strategy <S>` | `Highest` (default), `Lowest`, `MostCommon` or `Fail`. |
| `--interactive-conflicts` | Choose the version yourself, per conflict. |
| `--min-version` | Shorthand for `--conflict-strategy Lowest`. |
| `--ignore-prerelease` | Prefer stable versions when resolving conflicts. |
| `-t, --transitive-pinning` | Emit `CentralPackageTransitivePinningEnabled`. |
| `-e, --encoding <NAME>` | Write with this IANA encoding. Default: preserve each file's own. |
| `-l, --linewrap <STYLE>` | `lf`, `crlf` or `cr`. Default: preserve. |
| `-c, --version-comparison <S>` | `Default`, `Version`, `VersionRelease`, `VersionReleaseMetadata`. |
| `--unify-props` | Also hoist shared properties into `Directory.Build.props`. |
| `--no-backup`, `--backup-dir`, `--add-gitignore` | Backup behaviour. |
| `-f, --force` | Skip confirmation prompts. |

**Encoding and line endings are preserved by default.** A UTF-8-with-BOM, CRLF project file comes back byte-identical apart from the versions that were removed. Formatting, comments and attribute order survive too — the XML is edited surgically rather than re-serialised.

Version specified as a child element works as well as an attribute:

```xml
<PackageReference Include="Serilog">
  <Version>2.12.0</Version>
</PackageReference>
```

becomes `<PackageReference Include="Serilog" />`.

## `verify`

A migration is only correct if NuGet still resolves the *same* packages afterwards. `verify` proves it:

```bash
central-config verify -s MySolution.sln
```

1. Restores and snapshots the full resolved graph, transitive packages included.
2. Applies the migration.
3. Restores and snapshots again.
4. Diffs the two graphs.

If any package resolves to a different version, the run reports the drift and rolls the migration back. `--strict` also fails on pure additions (which are normal when transitive pinning is switched on). `--keep` keeps the migration in place and just reports. Exit code `9` means drift was found.

## `analyze`

```bash
central-config analyze [--audit] [--outdated] [--deprecated] [--licenses] [--transitive]
central-config analyze --all-checks            # all of the above
central-config analyze --fix                   # apply every auto-fixable finding
central-config analyze --fix-dry-run           # show what --fix would do
```

Offline rules always run. The four flags above add feed lookups; `--transitive` resolves the real graph through `dotnet list package`.

| Option | Meaning |
|---|---|
| `--fail-on <SEVERITY>` | Exit non-zero at or above this level. `Info` (default) … `Critical`, or `Never`. |
| `--rules <Rule=Severity>` | Re-grade a rule, or disable it with `Rule=none`. Repeatable. |
| `--baseline <PATH>` | Suppress findings recorded in a baseline. |
| `--write-baseline` | Record today's findings as accepted, so CI gates only on new ones. |
| `--ignore <PACKAGE>` | Skip a package entirely. Trailing `*` matches a prefix. Repeatable. |
| `--output <FORMAT>` | `Terminal` (default), `Json`, `Sarif`, `Markdown`, `Csv`. |
| `--output-file <PATH>` | Write the report to a file instead of stdout. |
| `--max-parallelism <N>` | Concurrent feed queries. Default 8. |

Every workspace gets a **health score out of 100** and a letter grade, weighted by severity and normalised against workspace size so a large solution is not punished twice for being large.

### Rules

Run `central-config explain all` for the live list, or `central-config explain <RuleId>` for the reasoning behind one.

| Rule | Default | Needs feed | Auto-fix |
|---|---|---|---|
| `SecurityVulnerability` | Critical | yes | yes |
| `MissingCentralVersion` | Critical | – | yes |
| `InlineVersionUnderCpm` | High | – | yes |
| `DeprecatedPackage` | High | yes | – |
| `UnpinnedTransitiveDependency` | High | yes | yes |
| `LicenseRisk` | Moderate | yes | – |
| `VersionInconsistency` | Moderate | – | yes |
| `TransitiveConflict` | Moderate | – | yes |
| `FloatingVersion` | Moderate | – | – |
| `RedundantReference` | Moderate | – | yes |
| `DuplicatePackageCasing` | Low | – | yes |
| `OutdatedPackage` | Low | yes | yes |
| `PrereleaseInProduction` | Low | – | yes |
| `FrameworkAlignment` | Low | – | – |
| `OrphanedPackageVersion` | Info | – | yes |
| `PropertyDrift` | Info | – | – |

Auto-fixes are planned in memory first, applied as one batch, and always backed up.

## `update`

```bash
central-config update --dry-run          # what is available
central-config update                    # update all, test, roll everything back on failure
central-config update --bisect           # keep the largest subset that still passes
central-config update --only Serilog,Polly
```

`--bisect` is the interesting one. When the full update set fails the tests, it splits the set, tests the halves, recurses, and keeps the largest combination that still goes green — then tells you exactly which packages it held back so you can look at them individually. `--bisect-budget` caps how many restore+test cycles it will spend (default 16), and `--bisect-test-filter` is passed straight to `dotnet test --filter`.

## `build`

Hoists MSBuild properties into `Directory.Build.props` — but only properties that **every project declaring them agrees on**, and never project-identity properties like `AssemblyName` or `RootNamespace`. Requiring unanimity is what makes the hoist behaviour-preserving. Conditioned `PropertyGroup`s are left alone.

```bash
central-config build --dry-run --diff
central-config build --property TargetFramework --property LangVersion
central-config build --keep-in-projects      # write the file, do not edit the projects
```

## Backups and rollback

Every mutating command copies the files it is about to touch into `.centralconfig-backups/<timestamp>/` with a manifest, before writing anything.

```bash
central-config backups list
central-config backups restore                # most recent
central-config backups restore 20250824-101500
central-config backups prune --retention 5
central-config backups prune --all
```

Files the operation *created* are recorded too, so a restore deletes them rather than leaving them behind. Pass `--add-gitignore` on any mutating command to keep the backup folder out of git.

---

## Configuration file

```bash
central-config init
```

writes a `.centralconfig.json`, discovered by walking up from the working directory the way `global.json` is. The command line always wins over the file.

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/TarasKovalenko/CentralConfigGenerator/main/schemas/centralconfig.schema.json",
  "conflictStrategy": "Highest",
  "backup": true,
  "addGitignore": true,
  "failOn": "High",
  "transitivePinning": false,
  "ignorePrerelease": false,
  "excludeDirs": "^(samples|benchmarks)$",
  "retention": { "enabled": true, "maxBackups": 5 },
  "rules": {
    "OutdatedPackage": "Low",
    "PropertyDrift": "none"
  },
  "ignorePackages": ["Internal.*"],
  "buildProperties": ["TargetFramework", "ImplicitUsings", "Nullable", "LangVersion"]
}
```

---

## Exit codes

| Code | Name | Meaning |
|---|---|---|
| 0 | Success | Done. |
| 1 | ValidationError | Bad arguments, or a file already exists without `--overwrite`. |
| 2 | FileOperationError | I/O or permission failure. |
| 3 | VersionConflict | Unresolvable conflict under `--conflict-strategy Fail`. |
| 4 | NoProjectsFound | Nothing to work on. |
| 5 | AnalysisIssuesFound | Findings at or above `--fail-on`. |
| 6 | UnexpectedError | Unhandled exception. |
| 7 | TestFailure | Tests failed during `update`; changes were rolled back. |
| 8 | IncompleteAnalysis | The scan could not finish — treat as "re-run", not "clean". |
| 9 | GraphDrift | `verify` found the package graph changed. |

---

## CI recipes

**Gate a PR on security findings, annotated inline:**

```yaml
- run: dotnet tool install --global CentralConfigGenerator
- run: |
    central-config analyze --audit --deprecated \
      --output Sarif --output-file report.sarif \
      --fail-on High --quiet
- uses: github/codeql-action/upload-sarif@v3
  if: always()
  with:
    sarif_file: report.sarif
```

**Put the report in the job summary:**

```yaml
- run: central-config analyze --all-checks --output Markdown --fail-on Never --quiet >> "$GITHUB_STEP_SUMMARY"
```

**Adopt gradually with a baseline:**

```bash
central-config analyze --all-checks --write-baseline   # accept today's debt
central-config analyze --all-checks --baseline .centralconfig-baseline.json --fail-on Moderate
```

Only *new* findings fail the build.

**Monorepo:**

```bash
central-config batch ./repos --parallel --continue --dry-run
```

---

## What you get

**Migration**

- `Directory.Packages.props` generated or merged into, `Directory.Build.props` generated from properties every project agrees on, and a `revert` that puts everything back.
- C#, F# and VB projects; `.sln`, `.slnx` and `.slnf` scoping; regex directory exclusion.
- Encoding, BOM, line endings, comments, indentation and attribute order preserved by default — or normalised on demand with `--encoding` and `--linewrap`.
- Versions declared as attributes or as child elements, plus `GlobalPackageReference` and `VersionOverride`.
- Conflict strategies: `Highest`, `Lowest`, `MostCommon`, `Fail`, or decide each one yourself.
- Transitive pinning, pre-release filtering, and a configurable version-comparison scope.

**Safety**

- Dry-run with real unified diffs, planned in memory so the preview and the write are the same code path.
- A timestamped backup before every write, with `restore`, retention and pruning — including deleting files the operation created.
- `verify`: migrate, then diff the resolved package graph before and after, and roll back automatically if anything moved.

**Analysis**

- 16 rules covering vulnerabilities, deprecation, licence risk, version drift, floating versions, transitive conflicts, duplicate and orphaned entries, framework and property drift.
- A 0–100 health score and letter grade, weighted by severity and normalised for workspace size.
- Per-rule severity overrides, package ignores, and baselines so CI gates only on new findings.
- Auto-fix for everything that can be fixed mechanically, batched and backed up.

**Maintenance**

- `update` moves central versions forward, runs the tests, and rolls back on failure — or bisects for the largest subset that still passes and names what it held back.
- `tree` for the resolved graph, `status` for a dashboard, `doctor` for environment diagnostics, `batch` for monorepos.

**Fits into CI**

- Terminal, JSON, SARIF 2.1.0, Markdown and CSV reports, to stdout or a file.
- Stable exit codes 0–9 so scripts can branch on the outcome.
- Private feeds through `nuget.config`; shell completions for bash, zsh, fish and PowerShell.
- Ships for `net8.0`, `net9.0` and `net10.0`.

---

## Safety model

- **Nothing is written during planning.** Dry-run, diff and apply all run the same planner; only the final step touches disk.
- **Backups first.** Every mutating command backs up before writing, including files it is about to create.
- **Unanimity for property hoisting.** A property moves up only when every project that declares it uses the same value.
- **Conditioned groups are untouched.** Per-configuration and per-framework `PropertyGroup`s never get hoisted or stripped.
- **Verification is a graph diff, not a file diff.** `verify` compares what NuGet actually resolves.
- **Incomplete is not clean.** If the feed was unreachable, the report says so and `analyze` exits `8` rather than pretending the workspace is healthy.

---

## Using the library

`CentralConfigGenerator.Core` is a normal library if you want to build on it:

```csharp
IFileSystem fileSystem = new PhysicalFileSystem();
var discovery = new ProjectDiscoveryService(fileSystem);

var migration = new MigrationService(
    discovery,
    fileSystem,
    new BackupService(fileSystem),
    new VersionSelector(new VersionConflictResolver()));

var plan = await migration.PlanAsync(new MigrationOptions { RootDirectory = "." });

foreach (var change in plan.Changes)
{
    Console.WriteLine(change.Diff());
}

await migration.ApplyAsync(plan, new MigrationOptions { RootDirectory = "." });
```

Key types: `MigrationService`, `AnalysisEngine`, `AutoFixService`, `VerificationService`, `PackageUpdateService`, `BackupService`, `ProjectDiscoveryService`, `NuGetFeedService`.

## Further reading

- [Migration playbook](docs/migration-playbook.md) — the full route from inline versions to a CI-gated workspace.
- [Architecture](docs/architecture.md) — how the pieces fit, and how to add a rule or a command.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Building the repository needs the .NET 10 SDK — it compiles all three target frameworks, so it is the only one you have to install.

```bash
dotnet build
dotnet test
```

## License

MIT — see [LICENSE](LICENSE).
