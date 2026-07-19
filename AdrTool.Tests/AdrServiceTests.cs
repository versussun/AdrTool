using Xunit;

namespace AdrTool.Tests;

public class AdrServiceTests
{
    [Fact]
    public void CreateNew_UsesSevenDigitPaddedNumberAndUnderscoreSlug()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());

        var path = service.CreateNew("Use Clean Architecture!");

        Assert.Equal("0000001-use_clean_architecture.md", Path.GetFileName(path));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void CreateNew_IncrementsNumberAcrossCalls()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());

        service.CreateNew("First");
        var second = service.CreateNew("Second");

        Assert.StartsWith("0000002-", Path.GetFileName(second));
    }

    [Fact]
    public void CreateNew_WithEmptyTitle_Throws()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());

        Assert.Throws<AdrToolException>(() => service.CreateNew("   "));
    }

    [Fact]
    public void CreateNew_DefaultTemplate_ContainsNumberTitleStatusDate()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());

        var content = File.ReadAllText(service.CreateNew("My decision"));

        Assert.Contains("# 0000001. My decision", content);
        Assert.Contains("- Status: Proposed", content);
        Assert.Contains("- Date: ", content);
    }

    [Fact]
    public void CreateNew_CustomTemplate_SupportsDateFormatTitleNumberEnvAndArgTokens()
    {
        using var dir = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(dir.Path, "templates"));
        File.WriteAllText(
            Path.Combine(dir.Path, "templates", "custom.md"),
            "{{Title:number}} | {{Date:dd.MM.yyyy}} | {{env:ADRTOOL_TEST_VAR}} | {{arg:author}}");

        Environment.SetEnvironmentVariable("ADRTOOL_TEST_VAR", "from-env");
        var service = new AdrService(dir.Path, new AdrConfig { TemplatePath = "templates/custom.md" });

        var content = File.ReadAllText(
            service.CreateNew("My decision", new Dictionary<string, string> { ["author"] = "Jane" }));

        Assert.Contains("0000001. My decision", content);
        Assert.Contains(DateTime.Now.ToString("dd.MM.yyyy"), content);
        Assert.Contains("from-env", content);
        Assert.Contains("Jane", content);
    }

    [Fact]
    public void CreateNew_UnknownToken_IsLeftAsIs()
    {
        using var dir = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(dir.Path, "templates"));
        File.WriteAllText(Path.Combine(dir.Path, "templates", "custom.md"), "{{NotAThing}}");
        var service = new AdrService(dir.Path, new AdrConfig { TemplatePath = "templates/custom.md" });

        var content = File.ReadAllText(service.CreateNew("Title"));

        Assert.Contains("{{NotAThing}}", content);
    }

    [Fact]
    public void ListAll_ReturnsRecordsOrderedByNumber()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("First");
        service.CreateNew("Second");

        var records = service.ListAll();

        Assert.Equal(2, records.Count);
        Assert.Equal(1, records[0].Number);
        Assert.Equal("First", records[0].Title);
        Assert.Equal(2, records[1].Number);
        Assert.Equal("Proposed", records[0].Status);
    }

    [Fact]
    public void ListAll_WithNoAdrDirectory_ReturnsEmpty()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig { Path = "does-not-exist" });

        Assert.Empty(service.ListAll());
    }

    [Fact]
    public void CreateSuperseding_RewritesOldStatusAndLinksNewAdr()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        var oldPath = service.CreateNew("Old decision");

        var newPath = service.CreateSuperseding(1, "New decision");

        var oldContent = File.ReadAllText(oldPath);
        var newContent = File.ReadAllText(newPath);
        Assert.Contains($"Superseded by {Path.GetFileName(newPath)}", oldContent);
        Assert.Contains($"Supersedes: {Path.GetFileName(oldPath)}", newContent);
    }

    [Fact]
    public void CreateSuperseding_UnknownNumber_Throws()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());

        Assert.Throws<AdrToolException>(() => service.CreateSuperseding(99, "New decision"));
    }

    [Fact]
    public void GenerateDashboard_DefaultRun_PreservesExistingRowsAndAppendsNew()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("First");
        service.GenerateDashboard();

        service.CreateSuperseding(1, "Second"); // changes ADR 1's status on disk
        var dashboardPath = service.GenerateDashboard(); // default: must NOT refresh ADR 1's row

        var content = File.ReadAllText(dashboardPath);
        Assert.Contains("| 0000001 |", content);
        Assert.Contains("Proposed", content); // stale status for #1 preserved
        Assert.Contains("0000002", content); // new row appended
        Assert.DoesNotContain("Superseded by", content);
    }

    [Fact]
    public void GenerateDashboard_Recreate_RefreshesStatus()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("First");
        service.GenerateDashboard();
        service.CreateSuperseding(1, "Second");

        var dashboardPath = service.GenerateDashboard(recreate: true);

        Assert.Contains("Superseded by", File.ReadAllText(dashboardPath));
    }

    [Fact]
    public void GenerateDashboard_WithNoAdrs_WritesPlaceholderMessage()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());

        var dashboardPath = service.GenerateDashboard();

        Assert.Contains("No ADRs yet.", File.ReadAllText(dashboardPath));
    }

    [Fact]
    public void CopyDefaultTemplateTo_CreatesFileAndParentDirectories()
    {
        using var dir = new TempDirectory();
        var destination = Path.Combine(dir.Path, "nested", "template.md");

        AdrService.CopyDefaultTemplateTo(destination);

        Assert.True(File.Exists(destination));
    }

    [Fact]
    public void CopyDefaultTemplateTo_WhenFileExists_Throws()
    {
        using var dir = new TempDirectory();
        var destination = Path.Combine(dir.Path, "template.md");
        File.WriteAllText(destination, "existing");

        Assert.Throws<AdrToolException>(() => AdrService.CopyDefaultTemplateTo(destination));
    }
}
