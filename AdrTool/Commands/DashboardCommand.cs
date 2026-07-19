namespace AdrTool.Commands;

/// <summary>
/// "adr dashboard [--recreate]" — writes an index.md linking to all ADRs, ordered by number.
/// By default only appends rows for ADRs not already listed; "--recreate" rebuilds it from scratch.
/// </summary>
public sealed class DashboardCommand : ICommand
{
    public string Name => "dashboard";

    public int Execute(string[] args, string basePath)
    {
        var recreate = args.Contains("--recreate");

        var config = AdrConfig.Load(basePath);
        var service = new AdrService(basePath, config);
        var created = service.GenerateDashboard(recreate);

        Console.WriteLine($"Dashboard written to {created}");
        return 0;
    }
}
