namespace AdrTool.Commands;

/// <summary>"adr new &lt;name&gt;" — creates a new ADR from the template.</summary>
public sealed class NewCommand : ICommand
{
    public string Name => "new";

    public int Execute(string[] args, string basePath)
    {
        // The ADR name comes from the args (with "--key=value" pairs pulled out for {{arg:key}}).
        var (title, customArgs) = CommandArgs.Parse(args);

        var config = AdrConfig.Load(basePath);
        var service = new AdrService(basePath, config);
        var created = service.CreateNew(title, customArgs);

        Console.WriteLine($"Created {created}");
        return 0;
    }
}
