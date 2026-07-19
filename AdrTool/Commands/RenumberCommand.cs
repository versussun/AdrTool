namespace AdrTool.Commands;

/// <summary>
/// "adr renumber [--check]" — resolves numbering gaps and duplicate ADR numbers by reassigning
/// sequential numbers (renaming files and rewriting cross-references to match). "--check" reports
/// what would change and exits non-zero without touching disk, for use as a CI gate.
/// </summary>
public sealed class RenumberCommand(AdrService service) : ICommand
{
    public string Name => "renumber";

    public int Execute(string[] args)
    {
        var check = args.Contains("--check");

        if (check)
        {
            var plan = service.PlanRenumber();
            if (plan.Count == 0)
            {
                Console.WriteLine("Numbering is already sequential; nothing to renumber.");
                return 0;
            }

            Console.Error.WriteLine($"Numbering is stale: {plan.Count} ADR(s) would be renumbered:");
            foreach (var change in plan)
                Console.Error.WriteLine($"  {change.OldFileName} -> {change.NewFileName}");
            return 1;
        }

        var changes = service.Renumber();
        if (changes.Count == 0)
        {
            Console.WriteLine("Numbering is already sequential; nothing to renumber.");
            return 0;
        }

        Console.WriteLine($"Renumbered {changes.Count} ADR(s):");
        foreach (var change in changes)
            Console.WriteLine($"  {change.OldFileName} -> {change.NewFileName}");
        Console.WriteLine("Run \"adr dashboard --recreate\" to refresh index.md with the new links.");

        return 0;
    }
}
