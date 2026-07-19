using System.Text;

namespace AdrTool.Commands;

/// <summary>
/// "adr completion &lt;bash|zsh&gt;" — prints a shell completion script to stdout. Users source it
/// (bash) or drop it into their fpath (zsh) to get tab-completion for subcommands and flags.
/// </summary>
public sealed class CompletionCommand : ICommand
{
    public string Name => "completion";

    // Kept in sync by hand with the commands registered in Program.cs (same convention as
    // HelpCommand's usage text, which is also hand-maintained rather than generated).
    private static readonly string[] CommandNames =
    [
        "init", "new", "list", "show", "edit", "search", "supersede", "accept", "reject",
        "link", "template", "dashboard", "lint", "renumber", "install-hooks", "config",
        "completion", "help",
    ];

    // Only commands with fixed, enumerable flags/subcommands are listed; free-form arguments
    // (e.g. "new"'s "--key=value") aren't completable and are left out.
    private static readonly (string Command, string[] Values)[] Flags =
    [
        ("list", ["--tag=", "--json"]),
        ("dashboard", ["--recreate", "--check", "--tag="]),
        ("link", ["--type="]),
        ("template", ["format", "copy"]),
        ("renumber", ["--check"]),
        ("install-hooks", ["--dashboard-check", "--force"]),
        ("config", ["--json"]),
        ("completion", ["bash", "zsh"]),
    ];

    public int Execute(string[] args)
    {
        var shell = args.Length > 0 ? args[0] : null;

        var script = shell switch
        {
            "bash" => BashScript(),
            "zsh" => ZshScript(),
            _ => throw new AdrToolException("Usage: adr completion <bash|zsh>"),
        };

        Console.WriteLine(script);
        return 0;
    }

    private static string BashScript()
    {
        var sb = new StringBuilder();
        sb.AppendLine("_adr_completions()");
        sb.AppendLine("{");
        sb.AppendLine("    local cur");
        sb.AppendLine("    COMPREPLY=()");
        sb.AppendLine("    cur=\"${COMP_WORDS[COMP_CWORD]}\"");
        sb.AppendLine();
        sb.AppendLine("    if [ \"$COMP_CWORD\" -eq 1 ]; then");
        sb.AppendLine("        COMPREPLY=( $(compgen -W \"" + string.Join(' ', CommandNames) + "\" -- \"$cur\") )");
        sb.AppendLine("        return 0");
        sb.AppendLine("    fi");
        sb.AppendLine();
        sb.AppendLine("    case \"${COMP_WORDS[1]}\" in");

        foreach (var (command, values) in Flags)
        {
            sb.AppendLine("        " + command + ")");
            sb.AppendLine("            COMPREPLY=( $(compgen -W \"" + string.Join(' ', values) + "\" -- \"$cur\") )");
            sb.AppendLine("            ;;");
        }

        sb.AppendLine("    esac");
        sb.AppendLine("}");
        sb.AppendLine("complete -F _adr_completions adr");

        return sb.ToString();
    }

    private static string ZshScript()
    {
        var sb = new StringBuilder();
        sb.AppendLine("#compdef adr");
        sb.AppendLine();
        sb.AppendLine("_adr()");
        sb.AppendLine("{");
        sb.AppendLine("    local -a commands");
        sb.AppendLine("    commands=(" + string.Join(' ', CommandNames) + ")");
        sb.AppendLine();
        sb.AppendLine("    if (( CURRENT == 2 )); then");
        sb.AppendLine("        compadd -a commands");
        sb.AppendLine("        return");
        sb.AppendLine("    fi");
        sb.AppendLine();
        sb.AppendLine("    case ${words[2]} in");

        foreach (var (command, values) in Flags)
        {
            sb.AppendLine("        " + command + ")");
            sb.AppendLine("            compadd " + string.Join(' ', values));
            sb.AppendLine("            ;;");
        }

        sb.AppendLine("    esac");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("_adr \"$@\"");

        return sb.ToString();
    }
}
