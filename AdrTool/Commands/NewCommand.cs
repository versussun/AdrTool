namespace AdrTool.Commands;

/// <summary>"adr new &lt;name&gt;" — creates a new ADR from the template.</summary>
public sealed class NewCommand(AdrService service) : ICommand
{
    public string Name => "new";

    public int Execute(string[] args)
    {
        // The ADR name comes from the args (with "--key=value" pairs pulled out for {{arg:key}}).
        var (title, customArgs) = CommandArgs.Parse(args);
        var tags = CommandArgs.ParseTags(customArgs);

        var created = service.CreateNew(title, customArgs, tags);

        Console.WriteLine($"Created {created}");
        return 0;
    }
}
