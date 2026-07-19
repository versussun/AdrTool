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
    }

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
}
