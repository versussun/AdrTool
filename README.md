# adr

A small CLI tool for managing Architecture Decision Records (ADRs) as numbered Markdown files, generated from a customizable template.

## Install

From the `AdrTool` project folder:

```bash
dotnet pack -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg AdrTool
```

This installs a global `adr` command (make sure `~/.dotnet/tools` is on your `PATH`).

## Tests

Unit tests live in `AdrTool.Tests` (xUnit):

```bash
cd AdrTool.Tests
dotnet test
```

## Quick start

```bash
adr new "Use PostgreSQL for storage"
# Created docs/adr/0000001-use_postgresql_for_storage.md   (or the current folder, if unconfigured)

adr list
# 0000001  Proposed              Use PostgreSQL for storage

adr supersede 1 "Use SQLite instead"
# rewrites ADR 1's Status line and creates a new ADR referencing it
```

`adr` always uses the folder it's run from as its base path.

## Commands

| Command | Description |
|---|---|
| `adr new <name> [--key=value ...]` | Create a new ADR from the template |
| `adr supersede <n> <name> [--key=value ...]` | Create a new ADR that supersedes ADR number `<n>` |
| `adr list` | List all ADRs (number, status, title) |
| `adr template format` | Show the available template placeholder tokens |
| `adr template copy` | Copy the bundled default template to the configured `templatePath` |
| `adr dashboard [--recreate]` | Add new ADRs to `index.md` (`--recreate` rebuilds it from scratch) |
| `adr help` / `adr -h` / `adr --help` | Show usage |

`--key=value` arguments can appear anywhere in `new`/`supersede` (interspersed with the title words) and are available in templates as `{{arg:key}}`.

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
| `{{env:VAR_NAME}}` | Value of environment variable `VAR_NAME` (empty if unset) |
| `{{arg:NAME}}` | Value of a `--NAME=value` argument passed on the command line (empty if not given) |

Unknown token names are left in the output as-is. The bundled default template lives at `AdrTool/templates/default.md`.

## Superseding an ADR

`adr supersede <n> <name>` creates a new ADR that records which ADR it supersedes (`{{Supersedes}}`), and rewrites the old ADR's `Status:` line to `Superseded by <new file>`.

## Dashboard

`adr dashboard` writes a Markdown table of every ADR (number, date added, status, title) ordered by number, with the title linking to its file, to `index.md` in the ADR directory (or wherever `dashboardPath` points — links are computed relative to that file's own location, so it works from any directory).

By default it only *adds* rows for ADRs not already listed — existing rows are left untouched, so a status change (e.g. from `adr supersede`) won't retroactively update a row already in the dashboard. Run `adr dashboard --recreate` to discard the file and rebuild every row from the ADRs' current state.

The "date added" column comes from the ADR file's own creation time (falling back to today if the filesystem doesn't track that) — not from the ADR's own `{{Date}}` content — since it records when the row was added to the dashboard, not when the ADR itself was written.
