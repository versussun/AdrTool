using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AdrTool;

/// <summary>A parsed ADR entry as shown by the "list" command.</summary>
public sealed record AdrRecord(int Number, string Title, string Status, string FilePath);

/// <summary>Creates and inspects ADR documents based on the configured template.</summary>
public sealed partial class AdrService(string basePath, AdrConfig config)
{
    public const int NumberPadding = 7;

    /// <summary>
    /// Creates a new ADR from the template and returns the path of the created file.
    /// </summary>
    public string CreateNew(string title, IReadOnlyDictionary<string, string>? customArgs = null)
        => CreateNew(title, customArgs, supersedesFileName: null);

    /// <summary>
    /// Creates a new ADR that supersedes an existing one: the new ADR records which ADR it
    /// supersedes, and the old ADR's status line is updated to point at the new file.
    /// </summary>
    public string CreateSuperseding(int oldNumber, string title, IReadOnlyDictionary<string, string>? customArgs = null)
    {
        var adrDirectory = config.ResolveAdrDirectory(basePath);
        var oldFile = FindByNumber(adrDirectory, oldNumber)
            ?? throw new AdrToolException($"No ADR found with number {oldNumber}");

        var newFilePath = CreateNew(title, customArgs, Path.GetFileName(oldFile));

        if (!TryMarkSuperseded(oldFile, Path.GetFileName(newFilePath)))
            Console.Error.WriteLine($"Warning: could not find a Status line in {oldFile}; it was left unchanged.");

        return newFilePath;
    }

    /// <summary>Lists all ADRs in the configured directory, ordered by number.</summary>
    public IReadOnlyList<AdrRecord> ListAll()
    {
        var adrDirectory = config.ResolveAdrDirectory(basePath);
        if (!Directory.Exists(adrDirectory))
            return [];

        var records = new List<AdrRecord>();
        foreach (var file in Directory.EnumerateFiles(adrDirectory, "*.md"))
        {
            var match = LeadingNumber().Match(Path.GetFileName(file));
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var number))
                continue;

            var content = File.ReadAllText(file);
            records.Add(new AdrRecord(number, ExtractTitle(content, file), ExtractStatus(content), file));
        }

        return records.OrderBy(r => r.Number).ToList();
    }

    /// <summary>
    /// Writes a Markdown dashboard listing all ADRs, ordered by number, with links to each file,
    /// to the configured dashboard path (see <see cref="AdrConfig.ResolveDashboardPath"/>).
    /// Returns the path written.
    ///
    /// By default this only adds rows for ADRs not already listed; existing rows (and the "date
    /// added" they recorded) are preserved as-is. Pass <paramref name="recreate"/> to discard the
    /// existing file and rebuild every row from the current state.
    /// </summary>
    public string GenerateDashboard(bool recreate = false)
    {
        var dashboardPath = config.ResolveDashboardPath(basePath);
        var dashboardDirectory = Path.GetDirectoryName(dashboardPath);
        if (!string.IsNullOrEmpty(dashboardDirectory))
            Directory.CreateDirectory(dashboardDirectory);

        var rows = recreate ? new Dictionary<int, string>() : ParseExistingRows(dashboardPath);

        foreach (var record in ListAll())
        {
            if (rows.ContainsKey(record.Number))
                continue;

            var numberText = record.Number.ToString($"D{NumberPadding}", CultureInfo.InvariantCulture);
            var relativeLink = Path.GetRelativePath(dashboardDirectory ?? basePath, record.FilePath).Replace('\\', '/');
            var dateAdded = DashboardDateFor(record.FilePath);
            rows[record.Number] =
                $"| {numberText} | {EscapeTableCell(dateAdded)} | {EscapeTableCell(record.Status)} | [{EscapeTableCell(record.Title)}]({relativeLink}) |";
        }

        var content = new StringBuilder()
            .AppendLine("# Architecture Decision Records")
            .AppendLine();

        if (rows.Count == 0)
        {
            content.AppendLine("No ADRs yet.");
        }
        else
        {
            content.AppendLine("| # | Date added | Status | Title |")
                .AppendLine("|---|---|---|---|");

            foreach (var row in rows.OrderBy(r => r.Key))
                content.AppendLine(row.Value);
        }

        File.WriteAllText(dashboardPath, content.ToString());
        return dashboardPath;
    }

    /// <summary>Reads number -> full row text for each ADR already listed in an existing dashboard.</summary>
    private static Dictionary<int, string> ParseExistingRows(string dashboardPath)
    {
        var rows = new Dictionary<int, string>();
        if (!File.Exists(dashboardPath))
            return rows;

        foreach (var line in File.ReadAllLines(dashboardPath))
        {
            var match = DashboardRowNumber().Match(line);
            if (match.Success && int.TryParse(match.Groups["num"].Value, out var number))
                rows[number] = line;
        }

        return rows;
    }

    /// <summary>
    /// The date an ADR should be recorded as "added to the dashboard": the file's creation time
    /// if the filesystem tracks it, otherwise today (the date it's being added right now).
    /// </summary>
    private static string DashboardDateFor(string filePath)
    {
        try
        {
            var creationTime = File.GetCreationTime(filePath);
            if (creationTime.Year > 1601) // sentinel .NET returns when creation time isn't supported
                return creationTime.ToString(DefaultDateFormat, CultureInfo.InvariantCulture);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return DateTime.Now.ToString(DefaultDateFormat, CultureInfo.InvariantCulture);
    }

    private static string EscapeTableCell(string value) => value.Replace("|", "\\|");

    private string CreateNew(string title, IReadOnlyDictionary<string, string>? customArgs, string? supersedesFileName)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new AdrToolException("An ADR name is required. Usage: adr new <name>");

        var adrDirectory = config.ResolveAdrDirectory(basePath);
        Directory.CreateDirectory(adrDirectory);

        var number = NextNumber(adrDirectory);
        var numberText = number.ToString($"D{NumberPadding}", CultureInfo.InvariantCulture);
        var slug = Slugify(title);
        var fileName = $"{numberText}-{slug}.md";
        var filePath = Path.Combine(adrDirectory, fileName);

        if (File.Exists(filePath))
            throw new AdrToolException($"ADR already exists: {filePath}");

        var content = RenderTemplate(title, numberText, supersedesFileName, customArgs);
        File.WriteAllText(filePath, content);

        return filePath;
    }

    /// <summary>
    /// Determines the next order number by scanning existing ADR files' leading number.
    /// </summary>
    private static int NextNumber(string adrDirectory)
    {
        var max = 0;
        foreach (var file in Directory.EnumerateFiles(adrDirectory, "*.md"))
        {
            var match = LeadingNumber().Match(Path.GetFileName(file));
            if (match.Success && int.TryParse(match.Groups[1].Value, out var value) && value > max)
                max = value;
        }

        return max + 1;
    }

    private static string? FindByNumber(string adrDirectory, int number)
    {
        if (!Directory.Exists(adrDirectory))
            return null;

        foreach (var file in Directory.EnumerateFiles(adrDirectory, "*.md"))
        {
            var match = LeadingNumber().Match(Path.GetFileName(file));
            if (match.Success && int.TryParse(match.Groups[1].Value, out var value) && value == number)
                return file;
        }

        return null;
    }

    private const string DefaultDateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Renders a template, replacing tokens of the form "{{Name}}" or "{{Name:format}}".
    /// "{{Date}}" accepts a .NET date format string, e.g. "{{Date:dd.MM.yyyy}}".
    /// "{{Title}}" is the title alone; "{{Title:number}}" prefixes it with the order number.
    /// "{{env:VAR_NAME}}" substitutes the value of environment variable VAR_NAME (empty if unset).
    /// "{{arg:NAME}}" substitutes a "--NAME=value" argument passed on the command line (empty if not given).
    /// Other tokens ignore the format part. Unknown token names are left untouched.
    /// </summary>
    private string RenderTemplate(
        string title, string numberText, string? supersedesFileName, IReadOnlyDictionary<string, string>? customArgs)
    {
        var template = LoadTemplate();
        var supersedesLine = supersedesFileName is null ? "" : $"\n- Supersedes: {supersedesFileName}";

        return Token().Replace(template, match =>
        {
            var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;

            return match.Groups["name"].Value.ToLowerInvariant() switch
            {
                "number" => numberText,
                "title" => format is not null && format.Equals("number", StringComparison.OrdinalIgnoreCase)
                    ? $"{numberText}. {title}"
                    : title,
                "status" => "Proposed",
                "supersedes" => supersedesLine,
                "date" => DateTime.Now.ToString(format ?? DefaultDateFormat, CultureInfo.InvariantCulture),
                "env" => format is not null ? Environment.GetEnvironmentVariable(format.Trim()) ?? "" : "",
                "arg" => format is not null && (customArgs?.TryGetValue(format.Trim(), out var value) ?? false)
                    ? value
                    : "",
                _ => match.Value,
            };
        });
    }

    private string LoadTemplate()
    {
        var customPath = config.ResolveTemplatePath(basePath);
        if (customPath is null)
            return File.ReadAllText(DefaultTemplatePath);

        if (File.Exists(customPath))
            return File.ReadAllText(customPath);

        Console.Error.WriteLine($"Warning: configured template not found at {customPath}.");
        Console.Error.Write("Copy the default template there now? [y/N] ");

        if (IsYes(Console.ReadLine()))
        {
            CopyDefaultTemplateTo(customPath);
            Console.Error.WriteLine($"Copied default template to {customPath}");
            return File.ReadAllText(customPath);
        }

        Console.Error.WriteLine("Continuing with the built-in default template for this ADR.");
        return File.ReadAllText(DefaultTemplatePath);
    }

    private static bool IsYes(string? answer)
        => answer is not null
            && (answer.Trim().Equals("y", StringComparison.OrdinalIgnoreCase)
                || answer.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase));

    /// <summary>Copies the bundled default template to <paramref name="destinationPath"/>, which must not already exist.</summary>
    public static void CopyDefaultTemplateTo(string destinationPath)
    {
        if (File.Exists(destinationPath))
            throw new AdrToolException($"Template already exists at {destinationPath}.");

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.Copy(DefaultTemplatePath, destinationPath);
    }

    private static string DefaultTemplatePath
        => Path.Combine(AppContext.BaseDirectory, "templates", "default.md");

    /// <summary>Rewrites the first "Status:" line of an ADR file to point at its replacement.</summary>
    private static bool TryMarkSuperseded(string filePath, string newFileName)
    {
        var content = File.ReadAllText(filePath);
        var statusLine = StatusLine();
        if (!statusLine.IsMatch(content))
            return false;

        var updated = statusLine.Replace(content, $"${{prefix}}Superseded by {newFileName}", 1);
        File.WriteAllText(filePath, updated);
        return true;
    }

    /// <summary>Best-effort extraction of the title from the first Markdown heading.</summary>
    private static string ExtractTitle(string content, string filePath)
    {
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!line.TrimStart().StartsWith('#'))
                continue;

            var heading = line.TrimStart('#', ' ', '\t');
            var match = HeadingNumberPrefix().Match(heading);
            return match.Success ? match.Groups["title"].Value.Trim() : heading.Trim();
        }

        return Path.GetFileNameWithoutExtension(filePath);
    }

    /// <summary>Best-effort extraction of the status value from a "Status:" line.</summary>
    private static string ExtractStatus(string content)
    {
        var match = StatusValue().Match(content);
        return match.Success ? match.Groups["value"].Value.Trim() : "Unknown";
    }

    /// <summary>Turns a free-form title into a lowercase, underscore-separated file slug.</summary>
    private static string Slugify(string title)
    {
        var lower = title.Trim().ToLowerInvariant();
        var builder = new StringBuilder(lower.Length);
        var lastWasUnderscore = false;

        foreach (var ch in lower)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasUnderscore = false;
            }
            else if (!lastWasUnderscore && builder.Length > 0)
            {
                builder.Append('_');
                lastWasUnderscore = true;
            }
        }

        var slug = builder.ToString().Trim('_');
        return slug.Length == 0 ? "adr" : slug;
    }

    [GeneratedRegex(@"\{\{\s*(?<name>\w+)\s*(?::\s*(?<format>[^}]*))?\s*\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex Token();

    [GeneratedRegex(@"^(\d+)")]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"^(?:ADR\s+)?\d+[.:]?\s*(?<title>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingNumberPrefix();

    [GeneratedRegex(@"(?im)^(?<prefix>[-\s]*Status:\s*).*$")]
    private static partial Regex StatusLine();

    [GeneratedRegex(@"(?im)^[-\s]*Status:\s*(?<value>.*)$")]
    private static partial Regex StatusValue();

    [GeneratedRegex(@"^\|\s*(?<num>\d+)\s*\|")]
    private static partial Regex DashboardRowNumber();
}
