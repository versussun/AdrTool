namespace AdrTool.Commands;

/// <summary>"adr show &lt;n&gt;" — prints an ADR's content to stdout by number.</summary>
public sealed class ShowCommand : ICommand
{
    public string Name => "show";

    public int Execute(string[] args, string basePath)
    {
        if (args.Length < 1 || !int.TryParse(args[0].TrimStart('#'), out var number))
            throw new AdrToolException("Usage: adr show <n>");

        var config = AdrConfig.Load(basePath);
        var service = new AdrService(basePath, config);

        Console.WriteLine(service.GetContent(number));
        return 0;
    }
}
