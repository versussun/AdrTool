namespace AdrTool.Commands;

/// <summary>"adr accept &lt;n&gt;" — marks an ADR's status as Accepted in place.</summary>
public sealed class AcceptCommand : ICommand
{
    public string Name => "accept";

    public int Execute(string[] args, string basePath)
        => StatusChangeCommand.Execute("accept", "Accepted", args, basePath);
}
