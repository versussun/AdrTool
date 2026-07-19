namespace AdrTool.Commands;

/// <summary>
/// "adr lint" — flags ADRs missing Status/Date, duplicate numbers, or supersede links pointing at
/// nonexistent files. Exits non-zero when issues are found, for use as a CI gate.
/// </summary>
public sealed class LintCommand : ICommand
{
    public string Name => "lint";

    public int Execute(string[] args, string basePath)
    {
        var config = AdrConfig.Load(basePath);
        var service = new AdrService(basePath, config);
        var issues = service.Lint();

        if (issues.Count == 0)
        {
            Console.WriteLine("No issues found.");
            return 0;
        }

        foreach (var issue in issues)
            Console.WriteLine($"{issue.Number.ToString($"D{AdrService.NumberPadding}")}  {Path.GetFileName(issue.FilePath)}  {issue.Message}");

        Console.Error.WriteLine($"{issues.Count} issue(s) found.");
        return 1;
    }
}
