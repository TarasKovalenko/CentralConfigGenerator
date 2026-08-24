# Migration playbook

A step-by-step route from a solution with inline package versions to one that is centrally managed, verified, and gated in CI. Every step is safe to stop at.

## 0. Check the ground

```bash
central-config doctor
```

Confirms the SDK version, whether a solution file scopes the run, whether the NuGet feeds are reachable, and whether backups are already ignored by git. Fix anything marked ✗ before continuing; ! items are suggestions.

```bash
central-config status
```

Tells you what is already in place and the single most useful next action.

## 1. Look before you leap

```bash
central-config migrate --dry-run --diff
```

Nothing is written. You get:

- the resolved version for every package,
- a table of version conflicts and how each was resolved,
- a unified diff for every file that would change.

If the conflict resolution is not what you want:

```bash
central-config migrate --dry-run --diff --conflict-strategy Lowest
central-config migrate --dry-run --diff --ignore-prerelease
central-config migrate --interactive-conflicts     # decide each one yourself
```

`--conflict-strategy Fail` refuses to guess at all and exits `3`, which is a reasonable CI setting once the migration has landed.

## 2. Migrate with proof

```bash
central-config verify -s MySolution.sln
```

This snapshots the resolved package graph, migrates, restores, snapshots again, and diffs. A clean result means NuGet resolves exactly the same packages as before — the change is a refactor, not a behaviour change.

If drift is reported, the migration is rolled back automatically. Common causes:

- a project used a version range that the single central pin narrows,
- two projects disagreed and one had to move,
- transitive pinning is on and now pins packages that were previously floating (these appear as *additions*, which `verify` treats as acceptable unless you pass `--strict`).

If you would rather migrate without the restore/verify overhead:

```bash
central-config migrate --add-gitignore
```

## 3. Undo, if needed

```bash
central-config backups list
central-config backups restore
```

Or go all the way back to inline versions:

```bash
central-config revert --dry-run --diff
central-config revert
```

## 4. Clean the properties too

```bash
central-config build --dry-run --diff
central-config build
```

Only properties that every declaring project agrees on are hoisted, so the evaluated build does not change. Properties that differ (a project on a different `TargetFramework`, say) are left where they are and reported by the `FrameworkAlignment` and `PropertyDrift` rules instead.

## 5. Find out what else is wrong

```bash
central-config analyze --all-checks
```

Then fix the mechanical part:

```bash
central-config analyze --all-checks --fix-dry-run
central-config analyze --all-checks --fix
```

What is left is the work that needs a human: deprecated packages that need a replacement, copyleft licences that need a decision, floating versions that need a policy.

## 6. Gate it in CI

Start permissive and tighten:

```bash
# Day 1: accept the current state, fail only on new problems.
central-config analyze --all-checks --write-baseline
git add .centralconfig-baseline.json
```

```yaml
# Every PR from then on:
- run: |
    central-config analyze --audit --deprecated --licenses \
      --baseline .centralconfig-baseline.json \
      --output Sarif --output-file report.sarif \
      --fail-on High --quiet
- uses: github/codeql-action/upload-sarif@v3
  if: always()
  with:
    sarif_file: report.sarif
```

Shrink the baseline over time. When it is empty, delete it and let `--fail-on` do the work.

## 7. Keep moving forward

```bash
central-config update --dry-run
central-config update --bisect
```

`--bisect` is what makes a large update tractable: instead of "17 updates, tests fail, revert everything", you get "14 applied, 3 held back — here they are".

## Monorepos

```bash
central-config batch ./repos --list             # what would be processed
central-config batch ./repos --dry-run
central-config batch ./repos --parallel --continue
```

Each solution is an independent workspace with its own props file and its own backup set.
