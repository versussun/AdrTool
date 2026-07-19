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
    public void SetStatus_RewritesStatusLine()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("First");

        var filePath = service.SetStatus(1, "Accepted");

        Assert.Contains("- Status: Accepted", File.ReadAllText(filePath));
    }

    [Fact]
    public void SetStatus_UnknownNumber_Throws()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());

        Assert.Throws<AdrToolException>(() => service.SetStatus(99, "Accepted"));
    }

    [Fact]
    public void Link_Related_AddsSymmetricLineToBothFiles()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        var first = service.CreateNew("First");
        var second = service.CreateNew("Second");

        service.Link(1, 2, "related");

        Assert.Contains($"- Related: {Path.GetFileName(second)}", File.ReadAllText(first));
        Assert.Contains($"- Related: {Path.GetFileName(first)}", File.ReadAllText(second));
    }

    [Fact]
    public void Link_Amends_AddsDirectionalLines()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        var first = service.CreateNew("First");
        var second = service.CreateNew("Second");

        service.Link(1, 2, "amends");

        Assert.Contains($"- Amends: {Path.GetFileName(second)}", File.ReadAllText(first));
        Assert.Contains($"- Amended by: {Path.GetFileName(first)}", File.ReadAllText(second));
    }

    [Fact]
    public void Link_SameNumberTwice_IsNoOp()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        var first = service.CreateNew("First");
        service.CreateNew("Second");

        service.Link(1, 2, "related");
        service.Link(1, 2, "related"); // should not duplicate the line

        var content = File.ReadAllText(first);
        var occurrences = content.Split("- Related:").Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void Link_ToItself_Throws()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("First");

        Assert.Throws<AdrToolException>(() => service.Link(1, 1, "related"));
    }

    [Fact]
    public void Link_UnknownType_Throws()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("First");
        service.CreateNew("Second");

        Assert.Throws<AdrToolException>(() => service.Link(1, 2, "bogus"));
    }

    [Fact]
    public void CreateInitialMetaAdr_CreatesAcceptedRecordArchitectureDecisionsAdr()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());

        var path = service.CreateInitialMetaAdr();
        var content = File.ReadAllText(path);

        Assert.Equal("0000001-record_architecture_decisions.md", Path.GetFileName(path));
        Assert.Contains("# 0000001. Record architecture decisions", content);
        Assert.Contains("- Status: Accepted", content);
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

    [Fact]
    public void GetContent_ReturnsFullFileContent()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        var path = service.CreateNew("My decision");

        var content = service.GetContent(1);

        Assert.Equal(File.ReadAllText(path), content);
    }

    [Fact]
    public void GetContent_UnknownNumber_Throws()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());

        Assert.Throws<AdrToolException>(() => service.GetContent(99));
    }

    [Fact]
    public void CreateNew_WithTags_WritesTagsLineAndPopulatesRecord()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());

        var path = service.CreateNew("My decision", tags: ["security", "infra"]);

        Assert.Contains("- Tags: security, infra", File.ReadAllText(path));
        var record = service.ListAll().Single();
        Assert.Equal(["security", "infra"], record.Tags);
    }

    [Fact]
    public void CreateNew_WithoutTags_HasNoTagsLineAndEmptyTagList()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());

        service.CreateNew("My decision");

        var record = service.ListAll().Single();
        Assert.Empty(record.Tags);
        Assert.DoesNotContain("Tags:", File.ReadAllText(Directory.GetFiles(dir.Path, "*.md")[0]));
    }

    [Fact]
    public void CreateSuperseding_WithTags_TagsTheNewAdr()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("Old decision");

        service.CreateSuperseding(1, "New decision", tags: ["security"]);

        var record = service.ListAll().Single(r => r.Number == 2);
        Assert.Equal(["security"], record.Tags);
    }

    [Fact]
    public void Search_MatchesTitleAndContentCaseInsensitively()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("Use PostgreSQL for storage");
        service.CreateNew("Adopt feature flags");

        var results = service.Search("POSTGRESQL");

        Assert.Single(results);
        Assert.Equal("Use PostgreSQL for storage", results[0].Record.Title);
    }

    [Fact]
    public void Search_ReturnsMatchingContentLines()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("First", tags: ["security"]);

        var results = service.Search("security");

        Assert.Single(results);
        Assert.Contains(results[0].MatchingLines, l => l.Contains("Tags: security"));
    }

    [Fact]
    public void Search_NoMatches_ReturnsEmpty()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("First");

        Assert.Empty(service.Search("nonexistent-keyword"));
    }

    [Fact]
    public void GenerateDashboard_WithTagFilter_OnlyAddsMatchingAdrs()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("First", tags: ["security"]);
        service.CreateNew("Second", tags: ["infra"]);

        var dashboardPath = service.GenerateDashboard(tag: "security");

        var content = File.ReadAllText(dashboardPath);
        Assert.Contains("| 0000001 |", content);
        Assert.DoesNotContain("| 0000002 |", content);
    }

    [Fact]
    public void Lint_WellFormedAdrs_ReturnsNoIssues()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("First");
        service.CreateSuperseding(1, "Second");

        Assert.Empty(service.Lint());
    }

    [Fact]
    public void Lint_MissingStatus_IsFlagged()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        var path = service.CreateNew("First");
        File.WriteAllText(path, File.ReadAllText(path).Replace("- Status: Proposed", ""));

        var issues = service.Lint();

        Assert.Contains(issues, i => i.Number == 1 && i.Message == "Missing Status");
    }

    [Fact]
    public void Lint_EmptyStatusValue_IsFlagged()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        var path = service.CreateNew("First");
        File.WriteAllText(path, File.ReadAllText(path).Replace("- Status: Proposed", "- Status:"));

        var issues = service.Lint();

        Assert.Contains(issues, i => i.Number == 1 && i.Message == "Missing Status");
    }

    [Fact]
    public void Lint_MissingDate_IsFlagged()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        var path = service.CreateNew("First");
        var withoutDate = string.Join(
            '\n', File.ReadAllText(path).Split('\n').Where(line => !line.TrimStart().StartsWith("- Date:")));
        File.WriteAllText(path, withoutDate);

        var issues = service.Lint();

        Assert.Contains(issues, i => i.Number == 1 && i.Message == "Missing Date");
    }

    [Fact]
    public void Lint_DuplicateNumbers_FlagsBothFiles()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        var first = service.CreateNew("First");
        File.Copy(first, Path.Combine(Path.GetDirectoryName(first)!, "0000001-duplicate.md"));

        var issues = service.Lint();

        Assert.Equal(2, issues.Count(i => i.Message.StartsWith("Duplicate ADR number")));
    }

    [Fact]
    public void Lint_SupersedesLinkToMissingFile_IsFlagged()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        var path = service.CreateNew("First");
        File.WriteAllText(path, File.ReadAllText(path).Replace(
            "- Date:", "- Supersedes: 0000099-nonexistent.md\n- Date:"));

        var issues = service.Lint();

        Assert.Contains(issues, i => i.Message.Contains("Supersede link points at nonexistent file"));
    }

    [Fact]
    public void Lint_SupersededByMissingFile_IsFlagged()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("First");
        service.SetStatus(1, "Superseded by 0000099-nonexistent.md");

        var issues = service.Lint();

        Assert.Contains(issues, i => i.Number == 1 && i.Message.Contains("Supersede link points at nonexistent file"));
    }

    [Fact]
    public void GetMissingDashboardEntries_ReturnsNumbersNotYetInDashboard()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("First");
        service.GenerateDashboard();
        service.CreateNew("Second");

        var missing = service.GetMissingDashboardEntries();

        Assert.Equal([2], missing);
    }

    [Fact]
    public void GetMissingDashboardEntries_UpToDate_ReturnsEmpty()
    {
        using var dir = new TempDirectory();
        var service = new AdrService(dir.Path, new AdrConfig());
        service.CreateNew("First");
        service.GenerateDashboard();

        Assert.Empty(service.GetMissingDashboardEntries());
    }
}
