# adr

A small CLI tool for managing Architecture Decision Records (ADRs) as numbered Markdown files, generated from a customizable template.

## Install

`AdrTool` is published on NuGet.org as [AdrTool](https://www.nuget.org/packages/AdrTool). It's a [.NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools), installable either globally (one shared install for your whole machine) or locally (pinned per-repo, so every contributor and CI run the exact same version).

### Global install

```bash
dotnet tool install --global AdrTool
```

This puts an `adr` command on your `PATH` (make sure `~/.dotnet/tools` is on it). Run it from anywhere:

```bash
adr new "My decision"
```

Upgrade later with `dotnet tool update --global AdrTool`.

### Local install (per-repo)

Local tools are pinned via a manifest file (`.config/dotnet-tools.json`) committed to the repo.

**1. Create the manifest** (once per repo, from its root, skip if `.config/dotnet-tools.json` already exists):

```bash
dotnet new tool-manifest
```

<details>
<summary>If that fails with an "Access to the path ... .templateengine ... is denied" error</summary>

Your local `~/.templateengine` cache is owned by a different user (often from a prior `sudo dotnet ...` run) — fix that ownership, or just write the manifest by hand instead:

```bash
mkdir -p .config
cat > .config/dotnet-tools.json <<'EOF'
{
  "version": 1,
  "isRoot": true,
  "tools": {}
}
EOF
```
</details>

**2. Install `adr` into that manifest, and commit both files:**

```bash
dotnet tool install --local AdrTool
git add .config/dotnet-tools.json
```

**3. Everyone else (or CI) just restores** — no extra setup needed, since NuGet.org is a default package source:

```bash
dotnet tool restore
```

**4. Run it** (a local install isn't on `PATH`, so it's invoked through `dotnet`):

```bash
dotnet adr new "My decision"
# or explicitly:
dotnet tool run adr new "My decision"
```

### Installing from source (unreleased changes)

To try a change that hasn't shipped to NuGet yet, pack the project locally and install from that folder instead of NuGet.org:

```bash
cd AdrTool && dotnet pack -c Release -o /path/to/local-feed

# global:
dotnet tool install --global --add-source /path/to/local-feed AdrTool
# or local, from the target repo:
dotnet tool install --local --add-source /path/to/local-feed AdrTool
```

## Tests

Unit tests live in `AdrTool.Tests` (xUnit):

```bash
cd AdrTool.Tests
dotnet test
```

## Quick start

```bash
adr init
# Created adr.config.json
# Created docs/adr/0000001-record_architecture_decisions.md

adr new "Use PostgreSQL for storage"
# Created docs/adr/0000002-use_postgresql_for_storage.md

adr list
# 0000001  Accepted              Record architecture decisions
# 0000002  Proposed              Use PostgreSQL for storage

adr supersede 2 "Use SQLite instead"
# rewrites ADR 2's Status line and creates a new ADR referencing it

adr accept 3
# marks ADR 3 as Accepted in place, no replacement needed

adr link 2 3 --type=amends
# records that ADR 3 amends ADR 2, without either one replacing the other

adr show 2
# prints ADR 2's full content to stdout

adr edit 2
# opens ADR 2 in $VISUAL/$EDITOR

adr config
# prints the effective configuration (resolved paths)

adr search postgresql
# greps titles/content across all ADRs and prints the matches

adr new "Rotate secrets automatically" --tags=security,infra
adr list --tag=security
# lists only ADRs tagged "security"
```

`adr` always uses the folder it's run from as its base path.

## Commands

| Command | Description |
|---|---|
| `adr init` | Bootstrap a repo: write `adr.config.json`, create the ADR directory, and create the first "Record architecture decisions" meta-ADR |
| `adr new <name> [--key=value ...]` | Create a new ADR from the template |
| `adr supersede <n> <name> [--key=value ...]` | Create a new ADR that supersedes ADR number `<n>` |
| `adr accept <n>` | Mark ADR number `<n>` as `Accepted` in place |
| `adr reject <n>` | Mark ADR number `<n>` as `Rejected` in place |
| `adr link <n> <m> [--type=related\|amends]` | Record a relationship between two existing ADRs (default: `related`) |
| `adr show <n>` | Print ADR number `<n>`'s content to stdout |
| `adr edit <n>` | Open ADR number `<n>` in `$VISUAL`/`$EDITOR` (falls back to a platform default) |
| `adr search <keyword>` | Search titles/content across all ADRs (case-insensitive) |
| `adr list [--tag=name] [--json]` | List all ADRs (number, status, title, tags), optionally filtered to those carrying `name`; `--json` prints a JSON array instead of the table |
| `adr template format` | Show the available template placeholder tokens |
| `adr template copy` | Copy the bundled default template to the configured `templatePath` |
| `adr dashboard [--recreate] [--check] [--tag=name]` | Add new ADRs to `index.md` (`--recreate` rebuilds it from scratch; `--check` exits non-zero without writing if the dashboard is stale; `--tag=name` restricts newly added rows to ADRs carrying that tag) |
| `adr lint` | Flag ADRs missing `Status`/`Date`, duplicate numbers, or supersede links pointing at nonexistent files; exits non-zero if any issues are found |
| `adr renumber [--check]` | Fix numbering gaps/duplicates by reassigning sequential numbers, renaming files and rewriting cross-references to match; `--check` reports what would change and exits non-zero without writing |
| `adr install-hooks [--dashboard-check] [--force]` | Install a git `pre-commit` hook that runs `adr lint` (and, with `--dashboard-check`, `adr dashboard --check`); refuses to overwrite an existing hook unless `--force` is given |
| `adr config [--json]` | Print the effective configuration — resolved ADR directory, template path, and dashboard path — after applying `adr.config.json` on top of the defaults |
| `adr completion <bash\|zsh>` | Print a shell completion script for subcommands and their flags |
| `adr help` / `adr -h` / `adr --help` | Show usage |

`--key=value` arguments can appear anywhere in `new`/`supersede` (interspersed with the title words) and are available in templates as `{{arg:key}}`. `--tags=a,b` is a first-class one of these: it records a comma-separated tag list on the ADR (see [Tags](#tags)).

## Configuration

Optional `adr.config.json` in the folder you run `adr` from:

```json
{
  "path": "docs/adr",
  "templatePath": "templates/adr-template.md",
  "dashboardPath": "docs/adr/index.md"
}
```

| Property | Description |
|---|---|
| `path` | Directory (relative to the current folder) where ADRs are stored. Defaults to the current folder. |
| `templatePath` | Template file (relative to the current folder) used for new ADRs. Defaults to the tool's built-in template. |
| `dashboardPath` | File (relative to the current folder) where `adr dashboard` writes its output. Defaults to `index.md` inside the ADR directory. |

If `templatePath` is set but the file doesn't exist, `adr new` warns and offers to copy the default template there interactively (or run `adr template copy` to do it non-interactively).

## Filenames

ADRs are named `NNNNNNN-slug.md` — a 7-digit, zero-padded order number, a hyphen, then the title lowercased with runs of non-alphanumeric characters collapsed to underscores. The next number is derived from the highest existing numbered file in the ADR directory.

## Template tokens

Templates are plain Markdown/text files using `{{Name}}` or `{{Name:format}}` tokens (case-insensitive). Run `adr template format` for the authoritative, in-tool reference; summary:

| Token | Result |
|---|---|
| `{{Number}}` | Order number, e.g. `0000001` |
| `{{Title}}` | The ADR title as typed on the command line |
| `{{Title:number}}` | Title prefixed with the order number, e.g. `0000001. My decision` |
| `{{Status}}` | Always `Proposed` for a new ADR |
| `{{Date}}` | Today's date, default format `yyyy-MM-dd` |
| `{{Date:format}}` | Today's date using a .NET date format string, e.g. `{{Date:dd.MM.yyyy}}` |
| `{{Supersedes}}` | Reference to the ADR being superseded (empty unless created via `adr supersede`) |
| `{{Tags}}` | A `- Tags: a, b` line from a `--tags=a,b` argument (empty if none given) |
| `{{env:VAR_NAME}}` | Value of environment variable `VAR_NAME` (empty if unset) |
| `{{arg:NAME}}` | Value of a `--NAME=value` argument passed on the command line (empty if not given) |

Unknown token names are left in the output as-is. The bundled default template lives at `AdrTool/templates/default.md`.

## Bootstrapping a repo

`adr init` sets up a fresh repo in one step: it writes `adr.config.json` (ADRs stored under `docs/adr`), creates that directory, and creates ADR `0000001`, titled "Record architecture decisions" — the meta-ADR convention almost every ADR tool ships, describing the decision to use ADRs at all (per Michael Nygard's original proposal). It's created already marked `Accepted`, and bypasses the template so its content is always the same regardless of any configured `templatePath`. Run it once per repo; it refuses to run again once `adr.config.json` exists.

## Changing status in place

`adr accept <n>` and `adr reject <n>` rewrite an ADR's `Status:` line to `Accepted`/`Rejected` without creating a replacement ADR — useful for the common case of a `Proposed` decision simply being approved or turned down as-is. Use `adr supersede` instead when a *new* decision is actually replacing the old one.

## Superseding an ADR

`adr supersede <n> <name>` creates a new ADR that records which ADR it supersedes (`{{Supersedes}}`), and rewrites the old ADR's `Status:` line to `Superseded by <new file>`.

## Linking related ADRs

Not every relationship between decisions is a supersession — `adr link <n> <m> [--type=related|amends]` records a relationship between two existing ADRs without changing either one's status:

- `--type=related` (the default) adds a symmetric `- Related: <file>` line to both ADRs.
- `--type=amends` adds a directional pair instead: `- Amends: <m's file>` on ADR `<n>` and `- Amended by: <n's file>` on ADR `<m>`.

Re-running the same link is a no-op rather than duplicating the line.

## Dashboard

`adr dashboard` writes a Markdown table of every ADR (number, date added, status, title) ordered by number, with the title linking to its file, to `index.md` in the ADR directory (or wherever `dashboardPath` points — links are computed relative to that file's own location, so it works from any directory).

By default it only *adds* rows for ADRs not already listed — existing rows are left untouched, so a status change (e.g. from `adr supersede`) won't retroactively update a row already in the dashboard. Run `adr dashboard --recreate` to discard the file and rebuild every row from the ADRs' current state. Pass `--tag=name` to only add rows for ADRs carrying that tag (combine with `--recreate` to rebuild the dashboard restricted to that tag entirely).

The "date added" column comes from the ADR file's own creation time (falling back to today if the filesystem doesn't track that) — not from the ADR's own `{{Date}}` content — since it records when the row was added to the dashboard, not when the ADR itself was written.

## Validation & CI integration

`adr lint` scans every ADR and flags:

- a missing (or empty) `Status:` line
- a missing (or empty) `Date:` line
- duplicate order numbers (two files sharing the same leading number)
- `Supersedes:` lines or `Superseded by <file>` status values that reference a file that doesn't exist

It prints one line per finding and exits `1` if anything was found, `0` otherwise — drop it into CI as a gate:

```bash
adr lint
```

`adr dashboard --check` complements the append/`--recreate` model above: instead of writing `index.md`, it exits non-zero if any ADR isn't reflected in it yet (also useful in CI, to catch a forgotten `adr dashboard` before merging):

```bash
adr dashboard --check
# Dashboard is stale: 1 ADR(s) not yet added: 0000003
```

It accepts `--tag=name` the same way `adr dashboard` does, and cannot be combined with `--recreate`.

`adr list --json` prints the same records as `adr list` as a JSON array (`number`, `title`, `status`, `filePath`, `tags`), for scripting or CI consumption instead of parsing the table:

```bash
adr list --json --tag=security
```

## Renumbering

`adr lint` only *flags* duplicate numbers; `adr renumber` fixes both duplicates and gaps by walking every ADR in order (current number, then filename) and reassigning sequential numbers starting at 1. For each file that moves, it:

- renames the file to its new `NNNNNNN-slug.md` name,
- rewrites the leading order number in that file's own heading, and
- rewrites `Supersedes:` / `Superseded by …` / `Related:` / `Amends:` / `Amended by:` references to it in *every* ADR, not just the ones being renumbered.

```bash
adr renumber
# Renumbered 1 ADR(s):
#   0000003-third.md -> 0000002-third.md
# Run "adr dashboard --recreate" to refresh index.md with the new links.

adr renumber --check
# reports the same plan and exits non-zero without writing anything — useful as a CI gate
```

Run `adr dashboard --recreate` afterward — renumbering doesn't rewrite an existing `index.md`, since dashboard rows are otherwise treated as append-only (see [Dashboard](#dashboard)).

## Git hooks

`adr install-hooks` writes a `pre-commit` hook to the repo's `.git/hooks` (resolving worktree/submodule `.git` files too) that runs `adr lint` before allowing a commit — add `--dashboard-check` to also gate on `adr dashboard --check`. It won't overwrite an existing `pre-commit` hook unless you pass `--force`. It detects a local-tool install (a `.config/dotnet-tools.json` manifest) and emits `dotnet adr ...` instead of `adr ...` in that case:

```bash
adr install-hooks
adr install-hooks --dashboard-check
adr install-hooks --force   # overwrite an existing pre-commit hook
```

## Shell completion

`adr completion bash` and `adr completion zsh` print a completion script to stdout, covering subcommand names and each command's fixed flags (free-form arguments like `new`'s `--key=value` aren't completable and are left out).

Bash — source it for the current session, or add it to your shell startup so it's always loaded:

```bash
source <(adr completion bash)

# persist it:
echo 'source <(adr completion bash)' >> ~/.bashrc
```

Zsh — drop it into a directory on your `fpath` as `_adr`, then start a new shell (or run `compinit`):

```bash
adr completion zsh > "${fpath[1]}/_adr"
```

## Finding ADRs

`adr show <n>` prints ADR number `<n>`'s full Markdown content to stdout — handy when you know the number but not the filename.

`adr edit <n>` opens ADR number `<n>`'s file in your editor: it uses `$VISUAL` if set, then `$EDITOR`, then falls back to `notepad` on Windows or `vi` elsewhere. The command waits for the editor to exit and returns its exit code. If your editor variable includes flags (e.g. `EDITOR="code --wait"`), they're passed through.

`adr search <keyword>` searches every ADR's title and content for `keyword` (case-insensitive) and prints each match's number/status/title along with up to three matching lines, e.g.:

```
adr search postgresql
# 0000002  Proposed              Use PostgreSQL for storage
#     # 0000002. Use PostgreSQL for storage
```

## Tags

`--tags=a,b` on `adr new`/`adr supersede` records a comma-separated tag list as a `- Tags: a, b` line in the ADR (via the `{{Tags}}` template token). Filter by tag with:

- `adr list --tag=security` — list only ADRs tagged `security`
- `adr dashboard --tag=security` — restrict which ADRs get added to the dashboard to those tagged `security`

Tags are plain text parsed from that line, so hand-editing an ADR's `Tags:` line works the same as setting it via `--tags`.
