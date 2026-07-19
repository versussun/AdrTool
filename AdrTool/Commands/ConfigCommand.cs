using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdrTool.Commands;

/// <summary>
/// "adr config [--json]" — prints the effective configuration, i.e. adr.config.json's values
/// resolved against their defaults, for debugging what "adr" will actually do in this folder.
/// </summary>
public sealed class ConfigCommand : ICommand
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
        string DashboardPath);

    public int Execute(string[] args, string basePath)
    {
        var config = AdrConfig.Load(basePath);
        var configFile = Path.Combine(basePath, AdrConfig.FileName);

        var resolved = new ResolvedConfig(
            ConfigFile: configFile,
            ConfigFileExists: File.Exists(configFile),
            AdrDirectory: config.ResolveAdrDirectory(basePath),
            TemplatePath: config.ResolveTemplatePath(basePath),
            DashboardPath: config.ResolveDashboardPath(basePath));

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(resolved, JsonOptions));
            return 0;
        }

        Console.WriteLine($"Config file:    {resolved.ConfigFile}{(resolved.ConfigFileExists ? "" : " (not found, using defaults)")}");
        Console.WriteLine($"ADR directory:  {resolved.AdrDirectory}");
        Console.WriteLine($"Template path:  {resolved.TemplatePath ?? "(built-in default)"}");
        Console.WriteLine($"Dashboard path: {resolved.DashboardPath}");

        return 0;
    }
}
