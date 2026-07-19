namespace AdrTool.Commands;

/// <summary>Splits raw CLI args into the free-form title text and "--key=value" custom template args.</summary>
public static class CommandArgs
{
    public static (string Title, IReadOnlyDictionary<string, string> CustomArgs) Parse(string[] args)
    {
        var customArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var titleWords = new List<string>();

        foreach (var arg in args)
        {
            var separatorIndex = arg.StartsWith("--", StringComparison.Ordinal) ? arg.IndexOf('=') : -1;
            if (separatorIndex > 2)
                customArgs[arg[2..separatorIndex]] = arg[(separatorIndex + 1)..];
            else
                titleWords.Add(arg);
        }

        return (string.Join(' ', titleWords).Trim(), customArgs);
    }
}
