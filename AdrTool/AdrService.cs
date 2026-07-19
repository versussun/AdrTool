using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AdrTool;

/// <summary>A parsed ADR entry as shown by the "list" command.</summary>
public sealed record AdrRecord(int Number, string Title, string Status, string FilePath, IReadOnlyList<string> Tags);

/// <summary>A single "adr search" match: the matched ADR and the lines within it that matched.</summary>
public sealed record AdrSearchResult(AdrRecord Record, IReadOnlyList<string> MatchingLines);

/// <summary>A single "adr lint" finding: a problem detected in an ADR file.</summary>
public sealed record AdrLintIssue(int Number, string FilePath, string Message);

/// <summary>Creates and inspects ADR documents based on the configured template.</summary>
public sealed partial class AdrService(string basePath, AdrConfig config)
{
    public const int NumberPadding = 7;

    /// <summary>
    /// Creates a new ADR from the template and returns the path of the created file.
    /// </summary>
    public string CreateNew(string title, IReadOnlyDictionary<string, string>? customArgs = null, IReadOnlyList<string>? tags = null)
        => CreateNew(title, customArgs, supersedesFileName: null, tags ?? []);

    /// <summary>
    /// Creates a new ADR that supersedes an existing one: the new ADR records which ADR it
    /// supersedes, and the old ADR's status line is updated to point at the new file.
    /// </summary>
    public string CreateSuperseding(
        int oldNumber, string title, IReadOnlyDictionary<string, string>? customArgs = null, IReadOnlyList<string>? tags = null)
    {
        var adrDirectory = config.ResolveAdrDirectory(basePath);
        var oldFile = FindByNumber(adrDirectory, oldNumber)
            ?? throw new AdrToolException($"No ADR found with number {oldNumber}");

        var newFilePath = CreateNew(title, customArgs, Path.GetFileName(oldFile), tags ?? []);

        if (!TryMarkSuperseded(oldFile, Path.GetFileName(newFilePath)))
            Console.Error.WriteLine($"Warning: could not find a Status line in {oldFile}; it was left unchanged.");

        return newFilePath;
    }

    /// <summary>
    /// Rewrites an existing ADR's Status line to the given value (e.g. "Accepted", "Rejected").
    /// Returns the path of the file that was updated.
    /// </summary>
    public string SetStatus(int number, string status)
    {
        var adrDirectory = config.ResolveAdrDirectory(basePath);
        var file = FindByNumber(adrDirectory, number)
            ?? throw new AdrToolException($"No ADR found with number {number}");

        if (!TrySetStatusLine(file, status))
            throw new AdrToolException($"Could not find a Status line in {file}.");

        return file;
    }

    /// <summary>Reads the full content of the ADR with the given number.</summary>
    public string GetContent(int number)
    {
        var adrDirectory = config.ResolveAdrDirectory(basePath);
        var file = FindByNumber(adrDirectory, number)
            ?? throw new AdrToolException($"No ADR found with number {number}");

        return File.ReadAllText(file);
    }

    /// <summary>
    /// Records a relationship between two existing ADRs by appending a reference line to each
    /// file's metadata block. "related" is symmetric (both files get "Related: &lt;other&gt;");
    /// "amends" is directional (the "from" ADR gets "Amends: &lt;to&gt;", the "to" ADR gets
    /// "Amended by: &lt;from&gt;"). Re-linking the same pair is a no-op.
    /// </summary>
    public void Link(int fromNumber, int toNumber, string type)
    {
        if (fromNumber == toNumber)
            throw new AdrToolException("Cannot link an ADR to itself.");

        var adrDirectory = config.ResolveAdrDirectory(basePath);
        var fromFile = FindByNumber(adrDirectory, fromNumber)
            ?? throw new AdrToolException($"No ADR found with number {fromNumber}");
        var toFile = FindByNumber(adrDirectory, toNumber)
            ?? throw new AdrToolException($"No ADR found with number {toNumber}");

        var (fromLabel, toLabel) = type.Trim().ToLowerInvariant() switch
        {
            "amends" => ("Amends", "Amended by"),
            "related" => ("Related", "Related"),
            _ => throw new AdrToolException($"Unknown link type '{type}'. Supported types: related, amends"),
        };

        if (!TryAddLinkLine(fromFile, fromLabel, Path.GetFileName(toFile)))
            Console.Error.WriteLine($"Warning: could not find a Date line in {fromFile}; it was left unchanged.");
        if (!TryAddLinkLine(toFile, toLabel, Path.GetFileName(fromFile)))
            Console.Error.WriteLine($"Warning: could not find a Date line in {toFile}; it was left unchanged.");
    }

    /// <summary>
    /// Creates the standard "Record architecture decisions" meta-ADR that "adr init" bootstraps
    /// a repo with, describing the ADR convention itself (Michael Nygard's original proposal).
    /// Bypasses the configured/default template, since this document's content is fixed.
    /// </summary>
    public string CreateInitialMetaAdr()
    {
        const string title = "Record architecture decisions";

        var adrDirectory = config.ResolveAdrDirectory(basePath);
        Directory.CreateDirectory(adrDirectory);

        var number = NextNumber(adrDirectory);
        var numberText = number.ToString($"D{NumberPadding}", CultureInfo.InvariantCulture);
        var filePath = Path.Combine(adrDirectory, $"{numberText}-{Slugify(title)}.md");

        if (File.Exists(filePath))
            throw new AdrToolException($"ADR already exists: {filePath}");

        var content =
            $"""
            # {numberText}. {title}

            - Status: Accepted
            - Date: {DateTime.Now.ToString(DefaultDateFormat, CultureInfo.InvariantCulture)}

            ## Context

            We need to record the architectural decisions made on this project.

            ## Decision

            We will use Architecture Decision Records, as described by Michael Nygard in this article: https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions

            ## Consequences

            See Michael Nygard's article, linked above.

            """;

        File.WriteAllText(filePath, content);
        return filePath;
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
            records.Add(ParseRecord(number, content, file));
        }

        return records.OrderBy(r => r.Number).ToList();
    }

    /// <summary>
    /// Searches every ADR's title and content for <paramref name="keyword"/> (case-insensitive),
    /// returning a match per ADR with the specific lines that matched.
    /// </summary>
    public IReadOnlyList<AdrSearchResult> Search(string keyword)
    {
        var adrDirectory = config.ResolveAdrDirectory(basePath);
        if (!Directory.Exists(adrDirectory))
            return [];

        var results = new List<AdrSearchResult>();
        foreach (var file in Directory.EnumerateFiles(adrDirectory, "*.md"))
        {
            var match = LeadingNumber().Match(Path.GetFileName(file));
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var number))
                continue;

            var content = File.ReadAllText(file);
            var record = ParseRecord(number, content, file);
            var matchingLines = content
                .Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingLines.Count == 0 && !record.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                continue;

            results.Add(new AdrSearchResult(record, matchingLines));
        }

        return results.OrderBy(r => r.Record.Number).ToList();
    }

    private static AdrRecord ParseRecord(int number, string content, string filePath)
        => new(number, ExtractTitle(content, filePath), ExtractStatus(content), filePath, ExtractTags(content));

    /// <summary>
    /// Scans all ADRs for common problems: missing Status/Date lines, duplicate order numbers,
    /// and Supersedes/"Superseded by" references pointing at files that don't exist. Returns an
    /// empty list when nothing is wrong.
    /// </summary>
    public IReadOnlyList<AdrLintIssue> Lint()
    {
        var adrDirectory = config.ResolveAdrDirectory(basePath);
        if (!Directory.Exists(adrDirectory))
            return [];

        var entries = new List<(int Number, string FilePath, string Content)>();
        foreach (var file in Directory.EnumerateFiles(adrDirectory, "*.md"))
        {
            var match = LeadingNumber().Match(Path.GetFileName(file));
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var number))
                continue;

            entries.Add((number, file, File.ReadAllText(file)));
        }

        var issues = new List<AdrLintIssue>();
        var fileNames = entries.Select(e => Path.GetFileName(e.FilePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var group in entries.GroupBy(e => e.Number))
        {
            if (group.Count() <= 1)
                continue;

            var siblings = string.Join(", ", group.Select(e => Path.GetFileName(e.FilePath)));
            foreach (var entry in group)
                issues.Add(new AdrLintIssue(entry.Number, entry.FilePath, $"Duplicate ADR number {entry.Number:D7} (used by: {siblings})"));
        }

        foreach (var entry in entries)
        {
            var statusMatch = StatusValue().Match(entry.Content);
            if (!statusMatch.Success || string.IsNullOrWhiteSpace(statusMatch.Groups["value"].Value))
                issues.Add(new AdrLintIssue(entry.Number, entry.FilePath, "Missing Status"));

            var dateMatch = DateValue().Match(entry.Content);
            if (!dateMatch.Success || string.IsNullOrWhiteSpace(dateMatch.Groups["value"].Value))
                issues.Add(new AdrLintIssue(entry.Number, entry.FilePath, "Missing Date"));

            foreach (var target in SupersedeReferences(entry.Content))
            {
                if (!fileNames.Contains(target))
                    issues.Add(new AdrLintIssue(entry.Number, entry.FilePath, $"Supersede link points at nonexistent file: {target}"));
            }
        }

        return issues.OrderBy(i => i.Number).ThenBy(i => i.Message, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Extracts the filenames referenced by a "- Supersedes: x" line and/or a Status line reading
    /// "Superseded by x", if present.
    /// </summary>
    private static IEnumerable<string> SupersedeReferences(string content)
    {
        var supersedesMatch = SupersedesValue().Match(content);
        if (supersedesMatch.Success)
        {
            var value = supersedesMatch.Groups["value"].Value.Trim();
            if (value.Length > 0)
                yield return value;
        }

        var statusMatch = StatusValue().Match(content);
        if (statusMatch.Success)
        {
            var value = statusMatch.Groups["value"].Value.Trim();
            if (value.StartsWith("Superseded by ", StringComparison.OrdinalIgnoreCase))
                yield return value["Superseded by ".Length..].Trim();
        }
    }

    /// <summary>
    /// Returns the numbers of ADRs not yet present in the dashboard at <see cref="AdrConfig.ResolveDashboardPath"/>
    /// (matching <paramref name="tag"/> if given), without writing anything. Used by "adr dashboard --check"
    /// as a CI gate for staleness.
    /// </summary>
    public IReadOnlyList<int> GetMissingDashboardEntries(string? tag = null)
    {
        var dashboardPath = config.ResolveDashboardPath(basePath);
        var existingRows = ParseExistingRows(dashboardPath);

        var missing = new List<int>();
        foreach (var record in ListAll())
        {
            if (existingRows.ContainsKey(record.Number))
                continue;
            if (tag is not null && !record.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                continue;

            missing.Add(record.Number);
        }

        return missing;
    }

    /// <summary>
    /// Writes a Markdown dashboard listing all ADRs, ordered by number, with links to each file,
    /// to the configured dashboard path (see <see cref="AdrConfig.ResolveDashboardPath"/>).
    /// Returns the path written.
    ///
    /// By default this only adds rows for ADRs not already listed; existing rows (and the "date
    /// added" they recorded) are preserved as-is. Pass <paramref name="recreate"/> to discard the
    /// existing file and rebuild every row from the current state. Pass <paramref name="tag"/> to
    /// only add rows for ADRs carrying that tag (already-listed rows are still preserved regardless
    /// of tag unless <paramref name="recreate"/> is also set).
    /// </summary>
    public string GenerateDashboard(bool recreate = false, string? tag = null)
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
            if (tag is not null && !record.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
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

    private string CreateNew(
        string title, IReadOnlyDictionary<string, string>? customArgs, string? supersedesFileName, IReadOnlyList<string> tags)
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

        var content = RenderTemplate(title, numberText, supersedesFileName, customArgs, tags);
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
    /// "{{Tags}}" substitutes a "- Tags: a, b, c" line from a "--tags=a,b,c" argument (empty if none given).
    /// Other tokens ignore the format part. Unknown token names are left untouched.
    /// </summary>
    private string RenderTemplate(
        string title,
        string numberText,
        string? supersedesFileName,
        IReadOnlyDictionary<string, string>? customArgs,
        IReadOnlyList<string> tags)
    {
        var template = LoadTemplate();
        var supersedesLine = supersedesFileName is null ? "" : $"\n- Supersedes: {supersedesFileName}";
        var tagsLine = tags.Count == 0 ? "" : $"\n- Tags: {string.Join(", ", tags)}";

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
                "tags" => tagsLine,
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
        => TrySetStatusLine(filePath, $"Superseded by {newFileName}");

    /// <summary>Rewrites the first "Status:" line of an ADR file to the given value.</summary>
    private static bool TrySetStatusLine(string filePath, string newValue)
    {
        var content = File.ReadAllText(filePath);
        var statusLine = StatusLine();
        if (!statusLine.IsMatch(content))
            return false;

        var updated = statusLine.Replace(content, $"${{prefix}}{newValue}", 1);
        File.WriteAllText(filePath, updated);
        return true;
    }

    /// <summary>Appends a "- Label: target" reference line after the first "Date:" line, unless already present.</summary>
    private static bool TryAddLinkLine(string filePath, string label, string targetFileName)
    {
        var content = File.ReadAllText(filePath);
        var newLine = $"- {label}: {targetFileName}";
        if (content.Contains(newLine, StringComparison.Ordinal))
            return true; // already linked; nothing to do

        var dateLine = DateLine();
        if (!dateLine.IsMatch(content))
            return false;

        var updated = dateLine.Replace(content, match => match.Value + "\n" + newLine, 1);
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

    /// <summary>Best-effort extraction of tags from a "- Tags: a, b, c" line.</summary>
    private static IReadOnlyList<string> ExtractTags(string content)
    {
        var match = TagsValue().Match(content);
        if (!match.Success)
            return [];

        return match.Groups["value"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

    [GeneratedRegex(@"(?im)^[-\s]*Status:[ \t]*(?<value>.*)$")]
    private static partial Regex StatusValue();

    [GeneratedRegex(@"(?im)^[-\s]*Date:.*$")]
    private static partial Regex DateLine();

    [GeneratedRegex(@"(?im)^[-\s]*Date:[ \t]*(?<value>.*)$")]
    private static partial Regex DateValue();

    [GeneratedRegex(@"(?im)^[-\s]*Tags:[ \t]*(?<value>.*)$")]
    private static partial Regex TagsValue();

    [GeneratedRegex(@"(?im)^[-\s]*Supersedes:[ \t]*(?<value>.*)$")]
    private static partial Regex SupersedesValue();

    [GeneratedRegex(@"^\|\s*(?<num>\d+)\s*\|")]
    private static partial Regex DashboardRowNumber();
}
