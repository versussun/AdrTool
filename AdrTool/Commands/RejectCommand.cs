namespace AdrTool.Commands;

/// <summary>"adr reject &lt;n&gt;" — marks an ADR's status as Rejected in place.</summary>
public sealed class RejectCommand(AdrService service) : ICommand
{
    public string Name => "reject";

    public int Execute(string[] args)
        => StatusChangeCommand.Execute("reject", "Rejected", args, service);
}
