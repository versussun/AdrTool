using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdrTool;

/// <summary>
/// Configuration loaded from adr.config.json located in the base path.
/// All properties are optional.
/// </summary>
public sealed class AdrConfig
{
    /// <summary>Directory (relative to the base path) where ADRs are stored.</summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>File (relative to the base path) holding the ADR template.</summary>
    [JsonPropertyName("templatePath")]
    public string? TemplatePath { get; set; }

    /// <summary>File (relative to the base path) where the dashboard is written.</summary>
    [JsonPropertyName("dashboardPath")]
    public string? DashboardPath { get; set; }

    /// <summary>
    /// Filename pattern for new ADRs. Supports a "{{Number}}" token (the padded order number) and a
    /// "{{Slug}}" token (the slugified title), which may carry a case variant, e.g. "{{Slug:pascal}}"
    /// or "{{Slug:kebab}}". "{{Number}}" must appear before "{{Slug}}". Defaults to "{{Number}}-{{Slug}}"
    /// (e.g. "0000001-use_clean_architecture.md").
    /// </summary>
    [JsonPropertyName("fileNameFormat")]
    public string? FileNameFormat { get; set; }

    /// <summary>Zero-padding width for order numbers in filenames and command output. Defaults to 7.</summary>
    [JsonPropertyName("numberPadding")]
    public int? NumberPadding { get; set; }

    /// <summary>
    /// Named override groups, selected via "--profile=name". Each overrides Path/TemplatePath/
    /// DashboardPath/FileNameFormat/NumberPadding.
    /// </summary>
    [JsonPropertyName("profiles")]
    public Dictionary<string, AdrProfileConfig>? Profiles { get; set; }

    public const string FileName = "adr.config.json";
    public const string DefaultFileNameFormat = "{{Number}}-{{Slug}}";
    public const int DefaultNumberPadding = 7;
    private const string DefaultDashboardFileName = "index.md";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Writes this configuration as <see cref="FileName"/> in <paramref name="basePath"/>.</summary>
    public void Save(string basePath)
    {
        var configFile = System.IO.Path.Combine(basePath, FileName);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configFile, json);
    }

    /// <summary>
    /// Loads the configuration from <see cref="FileName"/> in <paramref name="basePath"/>.
    /// Returns an empty config (all defaults) when the file does not exist.
    /// </summary>
    public static AdrConfig Load(string basePath)
    {
        var configFile = System.IO.Path.Combine(basePath, FileName);
        if (!File.Exists(configFile))
            return new AdrConfig();

        var json = File.ReadAllText(configFile);
        if (string.IsNullOrWhiteSpace(json))
            return new AdrConfig();

        try
        {
            return JsonSerializer.Deserialize<AdrConfig>(json, SerializerOptions) ?? new AdrConfig();
        }
        catch (JsonException ex)
        {
            throw new AdrToolException($"Failed to parse {FileName}: {ex.Message}");
        }
    }

    /// <summary>Absolute directory where ADRs live. Defaults to the base path.</summary>
    public string ResolveAdrDirectory(string basePath)
        => System.IO.Path.GetFullPath(System.IO.Path.Combine(basePath, Path ?? "."));

    /// <summary>Absolute path to the template file, or null when not configured.</summary>
    public string? ResolveTemplatePath(string basePath)
        => TemplatePath is null
            ? null
            : System.IO.Path.GetFullPath(System.IO.Path.Combine(basePath, TemplatePath));

    /// <summary>Absolute path to the dashboard file. Defaults to "index.md" inside the ADR directory.</summary>
    public string ResolveDashboardPath(string basePath)
        => DashboardPath is null
            ? System.IO.Path.Combine(ResolveAdrDirectory(basePath), DefaultDashboardFileName)
            : System.IO.Path.GetFullPath(System.IO.Path.Combine(basePath, DashboardPath));

    /// <summary>Effective filename pattern: <see cref="FileNameFormat"/> if set, otherwise <see cref="DefaultFileNameFormat"/>.</summary>
    public string ResolveFileNameFormat() => FileNameFormat ?? DefaultFileNameFormat;

    /// <summary>Effective order-number zero-padding width: <see cref="NumberPadding"/> if set, otherwise <see cref="DefaultNumberPadding"/>.</summary>
    public int ResolveNumberPadding() => NumberPadding ?? DefaultNumberPadding;

    /// <summary>
    /// Returns a config with the named profile's Path/TemplatePath/DashboardPath applied on top of
    /// this config's values (a profile that leaves a field unset falls back to this config's value).
    /// Returns this instance unchanged when <paramref name="name"/> is null. Profile name lookup is
    /// case-insensitive.
    /// </summary>
    public AdrConfig ForProfile(string? name)
    {
        if (name is null)
            return this;

        AdrProfileConfig? profile = null;
        if (Profiles is not null)
        {
            foreach (var (key, value) in Profiles)
            {
                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    profile = value;
                    break;
                }
            }
        }

        if (profile is null)
        {
            var available = Profiles is { Count: > 0 } ? string.Join(", ", Profiles.Keys) : "(none configured)";
            throw new AdrToolException($"No profile named '{name}' in {FileName}. Available profiles: {available}");
        }

        return new AdrConfig
        {
            Path = profile.Path ?? Path,
            TemplatePath = profile.TemplatePath ?? TemplatePath,
            DashboardPath = profile.DashboardPath ?? DashboardPath,
            FileNameFormat = profile.FileNameFormat ?? FileNameFormat,
            NumberPadding = profile.NumberPadding ?? NumberPadding,
            Profiles = Profiles,
        };
    }
}

/// <summary>A single named override group under "profiles" in adr.config.json.</summary>
public sealed class AdrProfileConfig
{
    /// <summary>Directory (relative to the base path) where this profile's ADRs are stored.</summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>File (relative to the base path) holding this profile's ADR template.</summary>
    [JsonPropertyName("templatePath")]
    public string? TemplatePath { get; set; }

    /// <summary>File (relative to the base path) where this profile's dashboard is written.</summary>
    [JsonPropertyName("dashboardPath")]
    public string? DashboardPath { get; set; }

    /// <summary>This profile's filename pattern for new ADRs. See <see cref="AdrConfig.FileNameFormat"/>.</summary>
    [JsonPropertyName("fileNameFormat")]
    public string? FileNameFormat { get; set; }

    /// <summary>This profile's order-number zero-padding width. See <see cref="AdrConfig.NumberPadding"/>.</summary>
    [JsonPropertyName("numberPadding")]
    public int? NumberPadding { get; set; }
}
