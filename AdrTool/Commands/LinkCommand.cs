namespace AdrTool.Commands;

/// <summary>
/// "adr link &lt;n&gt; &lt;m&gt; [--type=related|amends]" — records a relationship between two
/// existing ADRs without either one superseding the other. Defaults to a symmetric "related" link;
/// "--type=amends" records a directional "Amends" / "Amended by" pair instead.
/// </summary>
public sealed class LinkCommand(AdrService service) : ICommand
{
    public string Name => "link";

    public int Execute(string[] args)
    {
        var (numbers, customArgs) = CommandArgs.Parse(args);
        var parts = numbers.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2
            || !int.TryParse(parts[0].TrimStart('#'), out var fromNumber)
            || !int.TryParse(parts[1].TrimStart('#'), out var toNumber))
            throw new AdrToolException("Usage: adr link <n> <m> [--type=related|amends]");

        var type = customArgs.TryGetValue("type", out var value) ? value : "related";

        service.Link(fromNumber, toNumber, type);

        Console.WriteLine($"Linked ADR {fromNumber} and ADR {toNumber} ({type})");
        return 0;
    }
}
