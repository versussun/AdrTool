# 0000002. Configurable ADR file naming

- Status: Accepted
- Date: 2026-07-20

## Context

Filenames were hardcoded as `{NNNNNNN}-{lowercase_snake_slug}.md` (7-digit
padded number, dash, lowercase underscore slug), baked directly into
`AdrService.CreateNew`/`CreateInitialMetaAdr`. Some teams' conventions look
different, e.g. `ADR00001-Use_Clean_Architecture.md` (a literal prefix,
narrower padding, and Title_Case words), and there was no way to get that
without forking the tool.

The number-parsing regex relied on by `list`, `search`, `lint`, and
`renumber` assumed the filename started with bare digits (`^(\d+)`), so
supporting an arbitrary prefix before the number meant that regex had to
become format-aware rather than fixed.

## Decision

Add two optional `adr.config.json` properties (also overridable per
profile), both defaulting to today's exact behavior so existing configs are
unaffected:

- `fileNameFormat` (default `"{{Number}}-{{Slug}}"`) — reuses the same
  `{{Token}}` syntax already used in ADR templates. `{{Number}}` is the
  padded order number; `{{Slug}}` is the slugified title, optionally with a
  case variant (`{{Slug:pascal}}`, `{{Slug:kebab}}`, `{{Slug:upper}}`).
  `{{Number}}` must appear before `{{Slug}}`; the literal text around and
  between them (a prefix, a different separator, etc.) is preserved as-is.
- `numberPadding` (default `7`) — zero-padding width for the order number,
  used both in filenames and in command output (`list`/`search`/`lint`/
  `dashboard`).

`AdrService` derives the prefix and separator from the configured format
once per instance and builds a regex from them (`NumberPrefixRegex`) to
replace the old fixed `^(\d+)` pattern everywhere a filename's order number
needs to be recognized — `ListAll`, `Search`, `Lint`, `PlanRenumber`,
`NextNumber`, `FindByNumber`, and `StripLeadingNumber` (used by
`Renumber` to rebuild filenames under the new number while preserving the
configured prefix/separator).

## Consequences

- Teams can match their existing ADR naming convention (or adopt a new one)
  without forking the tool; `adr config` shows the effective format/padding.
- `fileNameFormat` must include exactly one `{{Number}}` and one `{{Slug}}`
  token, with `{{Number}}` first — an invalid format throws
  `AdrToolException` the first time it's used (on `new`, `list`, etc.),
  rather than being validated eagerly at config-load time.
- Changing `fileNameFormat`/`numberPadding` only affects newly created
  ADRs; existing files are not renamed automatically (`adr renumber` will
  rename files it touches to match the current format, but leaves
  untouched files as they are).
