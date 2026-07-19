using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdrTool.Commands;

/// <summary>
/// "adr list [--tag=name] [--json]" — lists all ADRs, optionally filtered to those carrying a given
/// tag. "--json" prints a JSON array instead of the table, for scripting/CI consumption.
/// </summary>
public sealed class ListCommand(AdrService service) : ICommand
{
    public string Name => "list";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public int Execute(string[] args)
    {
        var json = args.Contains("--json");
        var (_, customArgs) = CommandArgs.Parse(args);
        var tag = customArgs.TryGetValue("tag", out var value) ? value : null;

        var records = service.ListAll();

        if (tag is not null)
            records = records.Where(r => r.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(records, JsonOptions));
            return 0;
        }

        if (records.Count == 0)
        {
            Console.WriteLine(tag is null ? "No ADRs found." : $"No ADRs found with tag '{tag}'.");
            return 0;
        }

        foreach (var record in records)
        {
            var tagsSuffix = record.Tags.Count > 0 ? $"  [{string.Join(", ", record.Tags)}]" : "";
            Console.WriteLine($"{record.Number.ToString($"D{AdrService.NumberPadding}")}  {record.Status,-20}  {record.Title}{tagsSuffix}");
        }

        return 0;
    }
}
