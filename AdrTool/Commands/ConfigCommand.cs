using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdrTool.Commands;

/// <summary>
/// "adr config [--json]" — prints the effective configuration, i.e. adr.config.json's values
/// resolved against their defaults, for debugging what "adr" will actually do in this folder.
/// </summary>
public sealed class ConfigCommand(AdrConfig config, string basePath) : ICommand
{
    public string Name => "config";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed record ResolvedConfig(
        string ConfigFile,
        bool ConfigFileExists,
        string AdrDirectory,
        string? TemplatePath,
        string DashboardPath,
        IReadOnlyList<string> Profiles);

    public int Execute(string[] args)
    {
        var configFile = Path.Combine(basePath, AdrConfig.FileName);
        var profiles = config.Profiles is { Count: > 0 } ? config.Profiles.Keys.Order().ToList() : [];

        var resolved = new ResolvedConfig(
            ConfigFile: configFile,
            ConfigFileExists: File.Exists(configFile),
            AdrDirectory: config.ResolveAdrDirectory(basePath),
            TemplatePath: config.ResolveTemplatePath(basePath),
            DashboardPath: config.ResolveDashboardPath(basePath),
            Profiles: profiles);

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(resolved, JsonOptions));
            return 0;
        }

        Console.WriteLine($"Config file:    {resolved.ConfigFile}{(resolved.ConfigFileExists ? "" : " (not found, using defaults)")}");
        Console.WriteLine($"ADR directory:  {resolved.AdrDirectory}");
        Console.WriteLine($"Template path:  {resolved.TemplatePath ?? "(built-in default)"}");
        Console.WriteLine($"Dashboard path: {resolved.DashboardPath}");
        Console.WriteLine($"Profiles:       {(resolved.Profiles.Count == 0 ? "(none configured)" : string.Join(", ", resolved.Profiles))}");

        return 0;
    }
}
