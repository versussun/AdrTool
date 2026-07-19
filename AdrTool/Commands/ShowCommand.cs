namespace AdrTool.Commands;

/// <summary>"adr show &lt;n&gt;" — prints an ADR's content to stdout by number.</summary>
public sealed class ShowCommand(AdrService service) : ICommand
{
    public string Name => "show";

    public int Execute(string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0].TrimStart('#'), out var number))
            throw new AdrToolException("Usage: adr show <n>");

        Console.WriteLine(service.GetContent(number));
        return 0;
    }
}
