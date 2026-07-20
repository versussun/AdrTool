using Xunit;

namespace AdrTool.Tests;

public class AdrConfigTests
{
    [Fact]
    public void Load_WithNoConfigFile_ReturnsDefaults()
    {
        using var dir = new TempDirectory();

        var config = AdrConfig.Load(dir.Path);

        Assert.Null(config.Path);
        Assert.Null(config.TemplatePath);
        Assert.Null(config.DashboardPath);
        Assert.Null(config.FileNameFormat);
        Assert.Null(config.NumberPadding);
    }

    [Fact]
    public void ResolveFileNameFormat_DefaultsToNumberDashSlug()
        => Assert.Equal("{{Number}}-{{Slug}}", new AdrConfig().ResolveFileNameFormat());

    [Fact]
    public void ResolveFileNameFormat_UsesConfiguredValue()
        => Assert.Equal("ADR{{Number}}-{{Slug}}", new AdrConfig { FileNameFormat = "ADR{{Number}}-{{Slug}}" }.ResolveFileNameFormat());

    [Fact]
    public void ResolveNumberPadding_DefaultsToSeven()
        => Assert.Equal(7, new AdrConfig().ResolveNumberPadding());

    [Fact]
    public void ResolveNumberPadding_UsesConfiguredValue()
        => Assert.Equal(5, new AdrConfig { NumberPadding = 5 }.ResolveNumberPadding());

    [Fact]
    public void Load_ParsesConfiguredProperties()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(
            Path.Combine(dir.Path, AdrConfig.FileName),
            """{ "path": "docs/adr", "templatePath": "templates/t.md", "dashboardPath": "docs/adr/index.md" }""");

        var config = AdrConfig.Load(dir.Path);

        Assert.Equal("docs/adr", config.Path);
        Assert.Equal("templates/t.md", config.TemplatePath);
        Assert.Equal("docs/adr/index.md", config.DashboardPath);
    }

    [Fact]
    public void Load_WithInvalidJson_ThrowsAdrToolException()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(Path.Combine(dir.Path, AdrConfig.FileName), "{ not json");

        Assert.Throws<AdrToolException>(() => AdrConfig.Load(dir.Path));
    }

    [Fact]
    public void ResolveAdrDirectory_DefaultsToBasePath()
    {
        using var dir = new TempDirectory();
        var config = new AdrConfig();

        Assert.Equal(Path.GetFullPath(dir.Path), config.ResolveAdrDirectory(dir.Path));
    }

    [Fact]
    public void ResolveAdrDirectory_UsesConfiguredPathRelativeToBasePath()
    {
        using var dir = new TempDirectory();
        var config = new AdrConfig { Path = "docs/adr" };

        var expected = Path.GetFullPath(Path.Combine(dir.Path, "docs", "adr"));
        Assert.Equal(expected, config.ResolveAdrDirectory(dir.Path));
    }

    [Fact]
    public void ResolveTemplatePath_ReturnsNullWhenNotConfigured()
    {
        using var dir = new TempDirectory();

        Assert.Null(new AdrConfig().ResolveTemplatePath(dir.Path));
    }

    [Fact]
    public void ResolveDashboardPath_DefaultsToIndexMdInAdrDirectory()
    {
        using var dir = new TempDirectory();
        var config = new AdrConfig { Path = "docs/adr" };

        var expected = Path.GetFullPath(Path.Combine(dir.Path, "docs", "adr", "index.md"));
        Assert.Equal(expected, config.ResolveDashboardPath(dir.Path));
    }

    [Fact]
    public void ResolveDashboardPath_UsesConfiguredValue()
    {
        using var dir = new TempDirectory();
        var config = new AdrConfig { DashboardPath = "reports/dash.md" };

        var expected = Path.GetFullPath(Path.Combine(dir.Path, "reports", "dash.md"));
        Assert.Equal(expected, config.ResolveDashboardPath(dir.Path));
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsProperties()
    {
        using var dir = new TempDirectory();
        var config = new AdrConfig { Path = "docs/adr" };

        config.Save(dir.Path);
        var loaded = AdrConfig.Load(dir.Path);

        Assert.Equal("docs/adr", loaded.Path);
        Assert.True(File.Exists(Path.Combine(dir.Path, AdrConfig.FileName)));
    }

    [Fact]
    public void Load_ParsesProfiles()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(
            Path.Combine(dir.Path, AdrConfig.FileName),
            """
            {
              "path": "docs/adr",
              "templatePath": "templates/default.md",
              "profiles": {
                "rfc": { "path": "docs/rfc", "templatePath": "templates/rfc.md" }
              }
            }
            """);

        var config = AdrConfig.Load(dir.Path);

        Assert.NotNull(config.Profiles);
        Assert.Equal("docs/rfc", config.Profiles["rfc"].Path);
        Assert.Equal("templates/rfc.md", config.Profiles["rfc"].TemplatePath);
    }

    [Fact]
    public void ForProfile_WithNullName_ReturnsSameInstance()
    {
        var config = new AdrConfig { Path = "docs/adr" };

        Assert.Same(config, config.ForProfile(null));
    }

    [Fact]
    public void ForProfile_OverridesFieldsSetOnTheProfile()
    {
        var config = new AdrConfig
        {
            Path = "docs/adr",
            TemplatePath = "templates/default.md",
            DashboardPath = "docs/adr/index.md",
            Profiles = new Dictionary<string, AdrProfileConfig>
            {
                ["rfc"] = new() { Path = "docs/rfc", TemplatePath = "templates/rfc.md" },
            },
        };

        var resolved = config.ForProfile("rfc");

        Assert.Equal("docs/rfc", resolved.Path);
        Assert.Equal("templates/rfc.md", resolved.TemplatePath);
        Assert.Equal("docs/adr/index.md", resolved.DashboardPath); // falls back: profile didn't set it
    }

    [Fact]
    public void ForProfile_OverridesFileNameFormatAndNumberPadding()
    {
        var config = new AdrConfig
        {
            FileNameFormat = "{{Number}}-{{Slug}}",
            NumberPadding = 7,
            Profiles = new Dictionary<string, AdrProfileConfig>
            {
                ["rfc"] = new() { FileNameFormat = "RFC{{Number}}-{{Slug:pascal}}", NumberPadding = 4 },
            },
        };

        var resolved = config.ForProfile("rfc");

        Assert.Equal("RFC{{Number}}-{{Slug:pascal}}", resolved.FileNameFormat);
        Assert.Equal(4, resolved.NumberPadding);
    }

    [Fact]
    public void ForProfile_FallsBackToTopLevelFileNameFormatAndNumberPadding()
    {
        var config = new AdrConfig
        {
            FileNameFormat = "{{Number}}-{{Slug}}",
            NumberPadding = 7,
            Profiles = new Dictionary<string, AdrProfileConfig> { ["rfc"] = new() { Path = "docs/rfc" } },
        };

        var resolved = config.ForProfile("rfc");

        Assert.Equal("{{Number}}-{{Slug}}", resolved.FileNameFormat);
        Assert.Equal(7, resolved.NumberPadding);
    }

    [Fact]
    public void ForProfile_NameLookupIsCaseInsensitive()
    {
        var config = new AdrConfig
        {
            Profiles = new Dictionary<string, AdrProfileConfig> { ["RFC"] = new() { Path = "docs/rfc" } },
        };

        Assert.Equal("docs/rfc", config.ForProfile("rfc").Path);
    }

    [Fact]
    public void ForProfile_WithUnknownName_Throws()
    {
        var config = new AdrConfig
        {
            Profiles = new Dictionary<string, AdrProfileConfig> { ["rfc"] = new() { Path = "docs/rfc" } },
        };

        var ex = Assert.Throws<AdrToolException>(() => config.ForProfile("missing"));
        Assert.Contains("rfc", ex.Message);
    }

    [Fact]
    public void ForProfile_WithNoProfilesConfigured_Throws()
    {
        var config = new AdrConfig();

        Assert.Throws<AdrToolException>(() => config.ForProfile("rfc"));
    }
}
