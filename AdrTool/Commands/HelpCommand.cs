namespace AdrTool.Commands;

/// <summary>"adr help" (also "-h" / "--help") — prints usage.</summary>
public sealed class HelpCommand : ICommand
{
    public string Name => "help";

    public int Execute(string[] args, string basePath)
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
              adr new <name> [--key=value ...]             Create a new ADR from the template
              adr supersede <n> <name> [--key=value ...]   Create a new ADR that supersedes ADR number <n>
              adr list                                     List all ADRs
              adr template format                          Show available template placeholder tokens
              adr template copy                            Copy the default template to the configured templatePath
              adr dashboard [--recreate]                    Add new ADRs to index.md (--recreate rebuilds it from scratch)

            "--key=value" arguments are available in templates as "{{arg:key}}" (see "adr template format").

            Configuration (adr.config.json in the current folder):
              path          Directory where ADRs are stored (default: current folder)
              templatePath  Template file used for new ADRs (default: built-in template)
            """);
    }
}
