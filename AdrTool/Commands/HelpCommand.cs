namespace AdrTool.Commands;

/// <summary>"adr help" (also "-h" / "--help") — prints usage.</summary>
public sealed class HelpCommand : ICommand
{
    public string Name => "help";

    public int Execute(string[] args)
    {
        Print();
        return 0;
    }

    public static void Print()
    {
        Console.WriteLine(
            """
            adr - Architecture Decision Record tool

            Usage:
              adr init                                      Bootstrap a repo: config, ADR directory, and a first meta-ADR
              adr new <name> [--tags=a,b] [--key=value ...] Create a new ADR from the template
              adr supersede <n> <name> [--tags=a,b] [--key=value ...]  Create a new ADR that supersedes ADR number <n>
              adr accept <n>                                Mark ADR number <n> as Accepted
              adr reject <n>                                Mark ADR number <n> as Rejected
              adr link <n> <m> [--type=related|amends]     Record a relationship between two ADRs (default: related)
              adr show <n>                                  Print ADR number <n>'s content to stdout
              adr edit <n>                                   Open ADR number <n> in $VISUAL/$EDITOR
              adr search <keyword>                          Search titles/content across all ADRs
              adr list [--tag=name] [--json]                List all ADRs, optionally filtered by tag, or as JSON
              adr template format                          Show available template placeholder tokens
              adr template copy                            Copy the default template to the configured templatePath
              adr dashboard [--recreate] [--check] [--tag=name]  Add new ADRs to index.md (--recreate rebuilds it; --check exits non-zero if stale)
              adr lint                                      Flag ADRs missing Status/Date, duplicate numbers, or dead supersede links
              adr renumber [--check]                        Fix numbering gaps/duplicates by reassigning sequential numbers
              adr install-hooks [--dashboard-check] [--force]  Install a git pre-commit hook that runs adr lint
              adr config [--json]                           Print the effective configuration (resolved paths)
              adr completion <bash|zsh>                     Print a shell completion script

            "--key=value" arguments are available in templates as "{{arg:key}}" (see "adr template format").
            "--tags=a,b" records comma-separated tags on an ADR, usable with "adr list --tag=" / "adr dashboard --tag=".
            "adr lint", "adr dashboard --check", and "adr renumber --check" exit non-zero on failure, for use as CI gates.

            Configuration (adr.config.json in the current folder):
              path          Directory where ADRs are stored (default: current folder)
              templatePath  Template file used for new ADRs (default: built-in template)
            """);
    }
}
