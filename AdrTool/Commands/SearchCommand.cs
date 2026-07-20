namespace AdrTool.Commands;

/// <summary>"adr search &lt;keyword&gt;" — greps titles/content across all ADRs.</summary>
public sealed class SearchCommand(AdrService service) : ICommand
{
    public string Name => "search";

    public int Execute(string[] args)
    {
        var keyword = string.Join(' ', args).Trim();
        if (string.IsNullOrWhiteSpace(keyword))
            throw new AdrToolException("Usage: adr search <keyword>");

        var results = service.Search(keyword);

        if (results.Count == 0)
        {
            Console.WriteLine($"No ADRs matched '{keyword}'.");
            return 0;
        }

        foreach (var result in results)
        {
            var record = result.Record;
            Console.WriteLine($"{record.Number.ToString($"D{service.NumberPadding}")}  {record.Status,-20}  {record.Title}");

            foreach (var line in result.MatchingLines.Where(l => !string.IsNullOrWhiteSpace(l)).Take(3))
                Console.WriteLine($"    {line.Trim()}");
        }

        return 0;
    }
}
