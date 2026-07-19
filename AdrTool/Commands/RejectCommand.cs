namespace AdrTool.Commands;

/// <summary>"adr reject &lt;n&gt;" — marks an ADR's status as Rejected in place.</summary>
public sealed class RejectCommand : ICommand
{
    public string Name => "reject";

    public int Execute(string[] args, string basePath)
        => StatusChangeCommand.Execute("reject", "Rejected", args, basePath);
}
