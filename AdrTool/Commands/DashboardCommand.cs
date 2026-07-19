namespace AdrTool.Commands;

/// <summary>
/// "adr dashboard [--recreate] [--check] [--tag=name]" — writes an index.md linking to all ADRs,
/// ordered by number. By default only appends rows for ADRs not already listed; "--recreate"
/// rebuilds it from scratch. "--check" writes nothing and instead exits non-zero if any ADR isn't
/// yet reflected in the dashboard, for use as a CI gate. "--tag=name" restricts newly added rows
/// (or, with "--recreate"/"--check", the whole dashboard) to ADRs carrying that tag.
/// </summary>
public sealed class DashboardCommand(AdrService service) : ICommand
{
    public string Name => "dashboard";

    public int Execute(string[] args)
    {
        var recreate = args.Contains("--recreate");
        var check = args.Contains("--check");
        var (_, customArgs) = CommandArgs.Parse(args);
        var tag = customArgs.TryGetValue("tag", out var value) ? value : null;

        if (check)
        {
            if (recreate)
                throw new AdrToolException("--check cannot be combined with --recreate.");

            var missing = service.GetMissingDashboardEntries(tag);
            if (missing.Count == 0)
            {
                Console.WriteLine("Dashboard is up to date.");
                return 0;
            }

            var numbers = string.Join(", ", missing.Select(n => n.ToString($"D{AdrService.NumberPadding}")));
            Console.Error.WriteLine($"Dashboard is stale: {missing.Count} ADR(s) not yet added: {numbers}");
            return 1;
        }

        var created = service.GenerateDashboard(recreate, tag);

        Console.WriteLine($"Dashboard written to {created}");
        return 0;
    }
}
