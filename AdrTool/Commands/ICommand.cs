namespace AdrTool.Commands;

/// <summary>A single CLI command, e.g. "adr new".</summary>
public interface ICommand
{
    /// <summary>The name typed on the command line (e.g. "new").</summary>
    string Name { get; }

    int Execute(string[] args);
}
