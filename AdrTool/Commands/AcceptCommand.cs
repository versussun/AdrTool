namespace AdrTool.Commands;

/// <summary>"adr accept &lt;n&gt;" — marks an ADR's status as Accepted in place.</summary>
public sealed class AcceptCommand(AdrService service) : ICommand
{
    public string Name => "accept";

    public int Execute(string[] args)
        => StatusChangeCommand.Execute("accept", "Accepted", args, service);
}
