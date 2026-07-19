namespace AdrTool.Commands;

/// <summary>"adr supersede &lt;number&gt; &lt;name&gt;" — creates an ADR that supersedes an existing one.</summary>
public sealed class SupersedeCommand : ICommand
{
    public string Name => "supersede";

    public int Execute(string[] args, string basePath)
    {
        if (args.Length < 2 || !int.TryParse(args[0].TrimStart('#'), out var oldNumber))
            throw new AdrToolException("Usage: adr supersede <number> <name>");

        var (title, customArgs) = CommandArgs.Parse(args[1..]);
        var tags = CommandArgs.ParseTags(customArgs);

        var config = AdrConfig.Load(basePath);
        var service = new AdrService(basePath, config);
        var created = service.CreateSuperseding(oldNumber, title, customArgs, tags);

        Console.WriteLine($"Created {created}");
        return 0;
    }
}
