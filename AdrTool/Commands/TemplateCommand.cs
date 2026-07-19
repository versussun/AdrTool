namespace AdrTool.Commands;

/// <summary>"adr template format|copy" — template tooling subcommands.</summary>
public sealed class TemplateCommand : ICommand
{
    public string Name => "template";

    public int Execute(string[] args, string basePath)
    {
        var subcommand = args.Length > 0 ? args[0] : null;

        return subcommand switch
        {
            "format" => FormatCommand(),
            "copy" => CopyCommand(basePath),
            _ => throw new AdrToolException("Usage: adr template format | adr template copy"),
        };
    }

    private static int FormatCommand()
    {
        Console.WriteLine(
            """
            adr template format - available placeholder tokens

            Tokens use "{{Name}}" or "{{Name:format}}" and are case-insensitive.

              {{Number}}          Order number, e.g. 0000001
              {{Title}}           The ADR title as typed on the command line
              {{Title:number}}    Title prefixed with the order number, e.g. "0000001. My decision"
              {{Status}}          Always "Proposed" for a new ADR
              {{Date}}            Today's date, default format yyyy-MM-dd
              {{Date:format}}     Today's date using a .NET date format string, e.g. {{Date:dd.MM.yyyy}}
              {{Supersedes}}      Reference to the ADR being superseded (empty unless created via "adr supersede")
              {{env:VAR_NAME}}    Value of environment variable VAR_NAME (empty if unset)
              {{arg:NAME}}        Value of a "--NAME=value" argument passed to "adr new" (empty if not given)

            Unknown tokens are left in the output as-is.
            """);

        return 0;
    }

    private static int CopyCommand(string basePath)
    {
        var config = AdrConfig.Load(basePath);
        var templatePath = config.ResolveTemplatePath(basePath)
            ?? throw new AdrToolException($"No templatePath configured. Add \"templatePath\" to {AdrConfig.FileName} first.");

        AdrService.CopyDefaultTemplateTo(templatePath);
        Console.WriteLine($"Copied default template to {templatePath}");
        return 0;
    }
}
