using AdrTool.Commands;
using Xunit;

namespace AdrTool.Tests;

public class CommandsTests
{
    private static string CaptureOutput(Action action)
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return writer.ToString();
    }

    [Fact]
    public void NewCommand_CreatesAdrAndPrintsCreatedPath()
    {
        using var dir = new TempDirectory();

        var output = CaptureOutput(() =>
        {
            var exitCode = new NewCommand().Execute(["My", "decision"], dir.Path);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("Created", output);
        Assert.Single(Directory.GetFiles(dir.Path, "*.md"));
    }

    [Fact]
    public void NewCommand_WithNoTitle_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new NewCommand().Execute([], dir.Path));
    }

    [Fact]
    public void ListCommand_WithNoAdrs_PrintsNoAdrsFound()
    {
        using var dir = new TempDirectory();

        var output = CaptureOutput(() => new ListCommand().Execute([], dir.Path));

        Assert.Contains("No ADRs found.", output);
    }

    [Fact]
    public void SupersedeCommand_WithMissingArgs_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new SupersedeCommand().Execute(["1"], dir.Path));
    }

    [Fact]
    public void AcceptCommand_MarksAdrAsAccepted()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["First"], dir.Path);

        var output = CaptureOutput(() =>
        {
            var exitCode = new AcceptCommand().Execute(["1"], dir.Path);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("Accepted", output);
        Assert.Contains("- Status: Accepted", File.ReadAllText(Directory.GetFiles(dir.Path, "*.md")[0]));
    }

    [Fact]
    public void RejectCommand_MarksAdrAsRejected()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["First"], dir.Path);

        var output = CaptureOutput(() => new RejectCommand().Execute(["1"], dir.Path));

        Assert.Contains("Rejected", output);
        Assert.Contains("- Status: Rejected", File.ReadAllText(Directory.GetFiles(dir.Path, "*.md")[0]));
    }

    [Fact]
    public void AcceptCommand_WithNoArgs_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new AcceptCommand().Execute([], dir.Path));
    }

    [Fact]
    public void LinkCommand_AddsRelatedLineToBothAdrs()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["First"], dir.Path);
        new NewCommand().Execute(["Second"], dir.Path);

        var output = CaptureOutput(() =>
        {
            var exitCode = new LinkCommand().Execute(["1", "2"], dir.Path);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("Linked", output);
        var files = Directory.GetFiles(dir.Path, "*.md").OrderBy(f => f).ToArray();
        Assert.Contains("- Related:", File.ReadAllText(files[0]));
        Assert.Contains("- Related:", File.ReadAllText(files[1]));
    }

    [Fact]
    public void LinkCommand_WithMissingArgs_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new LinkCommand().Execute(["1"], dir.Path));
    }

    [Fact]
    public void InitCommand_CreatesConfigDirectoryAndMetaAdr()
    {
        using var dir = new TempDirectory();

        var output = CaptureOutput(() =>
        {
            var exitCode = new InitCommand().Execute([], dir.Path);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("Created", output);
        Assert.True(File.Exists(Path.Combine(dir.Path, AdrConfig.FileName)));
        Assert.True(File.Exists(Path.Combine(dir.Path, "docs", "adr", "0000001-record_architecture_decisions.md")));
    }

    [Fact]
    public void InitCommand_WhenAlreadyInitialized_Throws()
    {
        using var dir = new TempDirectory();
        new InitCommand().Execute([], dir.Path);

        Assert.Throws<AdrToolException>(() => new InitCommand().Execute([], dir.Path));
    }

    [Fact]
    public void TemplateCommand_Format_PrintsTokenReference()
    {
        using var dir = new TempDirectory();

        var output = CaptureOutput(() => new TemplateCommand().Execute(["format"], dir.Path));

        Assert.Contains("{{Title:number}}", output);
    }

    [Fact]
    public void TemplateCommand_UnknownSubcommand_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new TemplateCommand().Execute(["bogus"], dir.Path));
    }

    [Fact]
    public void TemplateCommand_Copy_WithNoConfiguredTemplatePath_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new TemplateCommand().Execute(["copy"], dir.Path));
    }

    [Fact]
    public void DashboardCommand_WritesIndexAndPrintsPath()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["First"], dir.Path);

        var output = CaptureOutput(() => new DashboardCommand().Execute([], dir.Path));

        Assert.Contains("Dashboard written to", output);
        Assert.True(File.Exists(Path.Combine(dir.Path, "index.md")));
    }

    [Fact]
    public void HelpCommand_PrintsUsage()
    {
        var output = CaptureOutput(() => new HelpCommand().Execute([], "."));

        Assert.Contains("adr - Architecture Decision Record tool", output);
    }

    [Fact]
    public void ShowCommand_PrintsAdrContent()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["My", "decision"], dir.Path);

        var output = CaptureOutput(() =>
        {
            var exitCode = new ShowCommand().Execute(["1"], dir.Path);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("# 0000001. My decision", output);
    }

    [Fact]
    public void ShowCommand_UnknownNumber_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new ShowCommand().Execute(["99"], dir.Path));
    }

    [Fact]
    public void ShowCommand_WithNoArgs_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new ShowCommand().Execute([], dir.Path));
    }

    [Fact]
    public void SearchCommand_FindsMatchingAdrAndPrintsMatchingLine()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["Use", "PostgreSQL"], dir.Path);
        new NewCommand().Execute(["Adopt", "feature", "flags"], dir.Path);

        var output = CaptureOutput(() => new SearchCommand().Execute(["PostgreSQL"], dir.Path));

        Assert.Contains("Use PostgreSQL", output);
        Assert.DoesNotContain("feature flags", output);
    }

    [Fact]
    public void SearchCommand_NoMatches_PrintsNoneMatchedMessage()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["First"], dir.Path);

        var output = CaptureOutput(() => new SearchCommand().Execute(["nonexistent"], dir.Path));

        Assert.Contains("No ADRs matched", output);
    }

    [Fact]
    public void SearchCommand_WithNoKeyword_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new SearchCommand().Execute([], dir.Path));
    }

    [Fact]
    public void NewCommand_WithTags_WritesTagsLine()
    {
        using var dir = new TempDirectory();

        new NewCommand().Execute(["My", "decision", "--tags=security,infra"], dir.Path);

        Assert.Contains("- Tags: security, infra", File.ReadAllText(Directory.GetFiles(dir.Path, "*.md")[0]));
    }

    [Fact]
    public void ListCommand_WithTagFilter_OnlyShowsMatchingAdrs()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["First", "--tags=security"], dir.Path);
        new NewCommand().Execute(["Second", "--tags=infra"], dir.Path);

        var output = CaptureOutput(() => new ListCommand().Execute(["--tag=security"], dir.Path));

        Assert.Contains("First", output);
        Assert.DoesNotContain("Second", output);
    }

    [Fact]
    public void ListCommand_WithTagFilterAndNoMatches_PrintsMessage()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["First"], dir.Path);

        var output = CaptureOutput(() => new ListCommand().Execute(["--tag=nonexistent"], dir.Path));

        Assert.Contains("No ADRs found with tag 'nonexistent'.", output);
    }

    [Fact]
    public void DashboardCommand_WithTagFilter_OnlyIncludesMatchingAdrs()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["First", "--tags=security"], dir.Path);
        new NewCommand().Execute(["Second", "--tags=infra"], dir.Path);

        new DashboardCommand().Execute(["--tag=security"], dir.Path);

        var content = File.ReadAllText(Path.Combine(dir.Path, "index.md"));
        Assert.Contains("| 0000001 |", content);
        Assert.DoesNotContain("| 0000002 |", content);
    }

    [Fact]
    public void DashboardCommand_Check_UpToDate_ReturnsZero()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["First"], dir.Path);
        new DashboardCommand().Execute([], dir.Path);

        var output = CaptureOutput(() =>
        {
            var exitCode = new DashboardCommand().Execute(["--check"], dir.Path);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("up to date", output);
        Assert.False(File.Exists(Path.Combine(dir.Path, "does-not-write.md")));
    }

    [Fact]
    public void DashboardCommand_Check_Stale_ReturnsNonZeroAndDoesNotWrite()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["First"], dir.Path);
        new DashboardCommand().Execute([], dir.Path);
        new NewCommand().Execute(["Second"], dir.Path);

        var dashboardContentBefore = File.ReadAllText(Path.Combine(dir.Path, "index.md"));

        Assert.Equal(1, new DashboardCommand().Execute(["--check"], dir.Path));
        Assert.Equal(dashboardContentBefore, File.ReadAllText(Path.Combine(dir.Path, "index.md")));
    }

    [Fact]
    public void DashboardCommand_CheckWithRecreate_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new DashboardCommand().Execute(["--check", "--recreate"], dir.Path));
    }

    [Fact]
    public void ListCommand_Json_PrintsJsonArray()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["My", "decision", "--tags=security"], dir.Path);

        var output = CaptureOutput(() => new ListCommand().Execute(["--json"], dir.Path));

        Assert.Contains("\"number\": 1", output);
        Assert.Contains("\"title\": \"My decision\"", output);
        Assert.Contains("\"security\"", output);
    }

    [Fact]
    public void ListCommand_Json_WithNoAdrs_PrintsEmptyArray()
    {
        using var dir = new TempDirectory();

        var output = CaptureOutput(() => new ListCommand().Execute(["--json"], dir.Path));

        Assert.Equal("[]", output.Trim());
    }

    [Fact]
    public void EditCommand_UnknownNumber_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new EditCommand().Execute(["99"], dir.Path));
    }

    [Fact]
    public void EditCommand_WithNoArgs_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new EditCommand().Execute([], dir.Path));
    }

    [Fact]
    public void ConfigCommand_WithNoConfigFile_PrintsDefaults()
    {
        using var dir = new TempDirectory();

        var output = CaptureOutput(() =>
        {
            var exitCode = new ConfigCommand().Execute([], dir.Path);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("not found, using defaults", output);
        Assert.Contains("(built-in default)", output);
        Assert.Contains(dir.Path, output);
    }

    [Fact]
    public void ConfigCommand_WithConfigFile_PrintsResolvedPaths()
    {
        using var dir = new TempDirectory();
        new InitCommand().Execute([], dir.Path);

        var output = CaptureOutput(() => new ConfigCommand().Execute([], dir.Path));

        Assert.DoesNotContain("not found, using defaults", output);
        Assert.Contains(Path.Combine(dir.Path, "docs", "adr"), output);
    }

    [Fact]
    public void ConfigCommand_Json_PrintsJsonObject()
    {
        using var dir = new TempDirectory();

        var output = CaptureOutput(() => new ConfigCommand().Execute(["--json"], dir.Path));

        Assert.Contains("\"configFileExists\": false", output);
        Assert.Contains("\"adrDirectory\"", output);
    }

    [Fact]
    public void LintCommand_NoIssues_ReturnsZero()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["First"], dir.Path);

        var output = CaptureOutput(() =>
        {
            var exitCode = new LintCommand().Execute([], dir.Path);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("No issues found.", output);
    }

    [Fact]
    public void LintCommand_MissingStatus_ReturnsNonZero()
    {
        using var dir = new TempDirectory();
        new NewCommand().Execute(["First"], dir.Path);
        var file = Directory.GetFiles(dir.Path, "*.md")[0];
        File.WriteAllText(file, File.ReadAllText(file).Replace("- Status: Proposed", ""));

        var output = CaptureOutput(() =>
        {
            var exitCode = new LintCommand().Execute([], dir.Path);
            Assert.Equal(1, exitCode);
        });

        Assert.Contains("Missing Status", output);
    }
}
