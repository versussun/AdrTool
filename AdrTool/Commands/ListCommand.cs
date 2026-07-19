namespace AdrTool.Commands;

/// <summary>"adr list" — lists all ADRs in the configured directory.</summary>
public sealed class ListCommand : ICommand
{
    public string Name => "list";

    public int Execute(string[] args, string basePath)
    {
        var config = AdrConfig.Load(basePath);
        var service = new AdrService(basePath, config);
        var records = service.ListAll();

        if (records.Count == 0)
        {
            Console.WriteLine("No ADRs found.");
            return 0;
        }

        foreach (var record in records)
            Console.WriteLine($"{record.Number.ToString($"D{AdrService.NumberPadding}")}  {record.Status,-20}  {record.Title}");

        return 0;
    }
}
