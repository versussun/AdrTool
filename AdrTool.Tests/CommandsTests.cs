using AdrTool.Commands;
using Xunit;

namespace AdrTool.Tests;

public class CommandsTests
{
    private static AdrService Service(string basePath) => new(basePath, AdrConfig.Load(basePath));

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
            var exitCode = new NewCommand(Service(dir.Path)).Execute(["My", "decision"]);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("Created", output);
        Assert.Single(Directory.GetFiles(dir.Path, "*.md"));
    }

    [Fact]
    public void NewCommand_WithNoTitle_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new NewCommand(Service(dir.Path)).Execute([]));
    }

    [Fact]
    public void ListCommand_WithNoAdrs_PrintsNoAdrsFound()
    {
        using var dir = new TempDirectory();

        var output = CaptureOutput(() => new ListCommand(Service(dir.Path)).Execute([]));

        Assert.Contains("No ADRs found.", output);
    }

    [Fact]
    public void SupersedeCommand_WithMissingArgs_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new SupersedeCommand(Service(dir.Path)).Execute(["1"]));
    }

    [Fact]
    public void AcceptCommand_MarksAdrAsAccepted()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First"]);

        var output = CaptureOutput(() =>
        {
            var exitCode = new AcceptCommand(Service(dir.Path)).Execute(["1"]);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("Accepted", output);
        Assert.Contains("- Status: Accepted", File.ReadAllText(Directory.GetFiles(dir.Path, "*.md")[0]));
    }

    [Fact]
    public void RejectCommand_MarksAdrAsRejected()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First"]);

        var output = CaptureOutput(() => new RejectCommand(Service(dir.Path)).Execute(["1"]));

        Assert.Contains("Rejected", output);
        Assert.Contains("- Status: Rejected", File.ReadAllText(Directory.GetFiles(dir.Path, "*.md")[0]));
    }

    [Fact]
    public void AcceptCommand_WithNoArgs_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new AcceptCommand(Service(dir.Path)).Execute([]));
    }

    [Fact]
    public void LinkCommand_AddsRelatedLineToBothAdrs()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First"]);
        new NewCommand(Service(dir.Path)).Execute(["Second"]);

        var output = CaptureOutput(() =>
        {
            var exitCode = new LinkCommand(Service(dir.Path)).Execute(["1", "2"]);
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

        Assert.Throws<AdrToolException>(() => new LinkCommand(Service(dir.Path)).Execute(["1"]));
    }

    [Fact]
    public void InitCommand_CreatesConfigDirectoryAndMetaAdr()
    {
        using var dir = new TempDirectory();

        var output = CaptureOutput(() =>
        {
            var exitCode = new InitCommand(dir.Path).Execute([]);
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
        new InitCommand(dir.Path).Execute([]);

        Assert.Throws<AdrToolException>(() => new InitCommand(dir.Path).Execute([]));
    }

    [Fact]
    public void TemplateCommand_Format_PrintsTokenReference()
    {
        using var dir = new TempDirectory();

        var output = CaptureOutput(() => new TemplateCommand(AdrConfig.Load(dir.Path), dir.Path).Execute(["format"]));

        Assert.Contains("{{Title:number}}", output);
    }

    [Fact]
    public void TemplateCommand_UnknownSubcommand_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new TemplateCommand(AdrConfig.Load(dir.Path), dir.Path).Execute(["bogus"]));
    }

    [Fact]
    public void TemplateCommand_Copy_WithNoConfiguredTemplatePath_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new TemplateCommand(AdrConfig.Load(dir.Path), dir.Path).Execute(["copy"]));
    }

    [Fact]
    public void DashboardCommand_WritesIndexAndPrintsPath()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First"]);

        var output = CaptureOutput(() => new DashboardCommand(Service(dir.Path)).Execute([]));

        Assert.Contains("Dashboard written to", output);
        Assert.True(File.Exists(Path.Combine(dir.Path, "index.md")));
    }

    [Fact]
    public void HelpCommand_PrintsUsage()
    {
        var output = CaptureOutput(() => new HelpCommand().Execute([]));

        Assert.Contains("adr - Architecture Decision Record tool", output);
    }

    [Fact]
    public void ShowCommand_PrintsAdrContent()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["My", "decision"]);

        var output = CaptureOutput(() =>
        {
            var exitCode = new ShowCommand(Service(dir.Path)).Execute(["1"]);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("# 0000001. My decision", output);
    }

    [Fact]
    public void ShowCommand_UnknownNumber_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new ShowCommand(Service(dir.Path)).Execute(["99"]));
    }

    [Fact]
    public void ShowCommand_WithNoArgs_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new ShowCommand(Service(dir.Path)).Execute([]));
    }

    [Fact]
    public void SearchCommand_FindsMatchingAdrAndPrintsMatchingLine()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["Use", "PostgreSQL"]);
        new NewCommand(Service(dir.Path)).Execute(["Adopt", "feature", "flags"]);

        var output = CaptureOutput(() => new SearchCommand(Service(dir.Path)).Execute(["PostgreSQL"]));

        Assert.Contains("Use PostgreSQL", output);
        Assert.DoesNotContain("feature flags", output);
    }

    [Fact]
    public void SearchCommand_NoMatches_PrintsNoneMatchedMessage()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First"]);

        var output = CaptureOutput(() => new SearchCommand(Service(dir.Path)).Execute(["nonexistent"]));

        Assert.Contains("No ADRs matched", output);
    }

    [Fact]
    public void SearchCommand_WithNoKeyword_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new SearchCommand(Service(dir.Path)).Execute([]));
    }

    [Fact]
    public void NewCommand_WithTags_WritesTagsLine()
    {
        using var dir = new TempDirectory();

        new NewCommand(Service(dir.Path)).Execute(["My", "decision", "--tags=security,infra"]);

        Assert.Contains("- Tags: security, infra", File.ReadAllText(Directory.GetFiles(dir.Path, "*.md")[0]));
    }

    [Fact]
    public void ListCommand_WithTagFilter_OnlyShowsMatchingAdrs()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First", "--tags=security"]);
        new NewCommand(Service(dir.Path)).Execute(["Second", "--tags=infra"]);

        var output = CaptureOutput(() => new ListCommand(Service(dir.Path)).Execute(["--tag=security"]));

        Assert.Contains("First", output);
        Assert.DoesNotContain("Second", output);
    }

    [Fact]
    public void ListCommand_WithTagFilterAndNoMatches_PrintsMessage()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First"]);

        var output = CaptureOutput(() => new ListCommand(Service(dir.Path)).Execute(["--tag=nonexistent"]));

        Assert.Contains("No ADRs found with tag 'nonexistent'.", output);
    }

    [Fact]
    public void DashboardCommand_WithTagFilter_OnlyIncludesMatchingAdrs()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First", "--tags=security"]);
        new NewCommand(Service(dir.Path)).Execute(["Second", "--tags=infra"]);

        new DashboardCommand(Service(dir.Path)).Execute(["--tag=security"]);

        var content = File.ReadAllText(Path.Combine(dir.Path, "index.md"));
        Assert.Contains("| 0000001 |", content);
        Assert.DoesNotContain("| 0000002 |", content);
    }

    [Fact]
    public void DashboardCommand_Check_UpToDate_ReturnsZero()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First"]);
        new DashboardCommand(Service(dir.Path)).Execute([]);

        var output = CaptureOutput(() =>
        {
            var exitCode = new DashboardCommand(Service(dir.Path)).Execute(["--check"]);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("up to date", output);
        Assert.False(File.Exists(Path.Combine(dir.Path, "does-not-write.md")));
    }

    [Fact]
    public void DashboardCommand_Check_Stale_ReturnsNonZeroAndDoesNotWrite()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First"]);
        new DashboardCommand(Service(dir.Path)).Execute([]);
        new NewCommand(Service(dir.Path)).Execute(["Second"]);

        var dashboardContentBefore = File.ReadAllText(Path.Combine(dir.Path, "index.md"));

        Assert.Equal(1, new DashboardCommand(Service(dir.Path)).Execute(["--check"]));
        Assert.Equal(dashboardContentBefore, File.ReadAllText(Path.Combine(dir.Path, "index.md")));
    }

    [Fact]
    public void DashboardCommand_CheckWithRecreate_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new DashboardCommand(Service(dir.Path)).Execute(["--check", "--recreate"]));
    }

    [Fact]
    public void ListCommand_Json_PrintsJsonArray()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["My", "decision", "--tags=security"]);

        var output = CaptureOutput(() => new ListCommand(Service(dir.Path)).Execute(["--json"]));

        Assert.Contains("\"number\": 1", output);
        Assert.Contains("\"title\": \"My decision\"", output);
        Assert.Contains("\"security\"", output);
    }

    [Fact]
    public void ListCommand_Json_WithNoAdrs_PrintsEmptyArray()
    {
        using var dir = new TempDirectory();

        var output = CaptureOutput(() => new ListCommand(Service(dir.Path)).Execute(["--json"]));

        Assert.Equal("[]", output.Trim());
    }

    [Fact]
    public void EditCommand_UnknownNumber_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new EditCommand(Service(dir.Path)).Execute(["99"]));
    }

    [Fact]
    public void EditCommand_WithNoArgs_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new EditCommand(Service(dir.Path)).Execute([]));
    }

    [Fact]
    public void ConfigCommand_WithNoConfigFile_PrintsDefaults()
    {
        using var dir = new TempDirectory();

        var output = CaptureOutput(() =>
        {
            var exitCode = new ConfigCommand(AdrConfig.Load(dir.Path), dir.Path).Execute([]);
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
        new InitCommand(dir.Path).Execute([]);

        var output = CaptureOutput(() => new ConfigCommand(AdrConfig.Load(dir.Path), dir.Path).Execute([]));

        Assert.DoesNotContain("not found, using defaults", output);
        Assert.Contains(Path.Combine(dir.Path, "docs", "adr"), output);
    }

    [Fact]
    public void ConfigCommand_Json_PrintsJsonObject()
    {
        using var dir = new TempDirectory();

        var output = CaptureOutput(() => new ConfigCommand(AdrConfig.Load(dir.Path), dir.Path).Execute(["--json"]));

        Assert.Contains("\"configFileExists\": false", output);
        Assert.Contains("\"adrDirectory\"", output);
    }

    [Fact]
    public void RenumberCommand_AlreadySequential_ReturnsZero()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First"]);

        var output = CaptureOutput(() =>
        {
            var exitCode = new RenumberCommand(Service(dir.Path)).Execute([]);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("nothing to renumber", output);
    }

    [Fact]
    public void RenumberCommand_WithGap_RenamesFilesAndReturnsZero()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First"]);
        new NewCommand(Service(dir.Path)).Execute(["Second"]);
        new NewCommand(Service(dir.Path)).Execute(["Third"]);
        File.Delete(Directory.GetFiles(dir.Path, "0000002-*.md")[0]); // gap: #1 and #3 remain

        var output = CaptureOutput(() =>
        {
            var exitCode = new RenumberCommand(Service(dir.Path)).Execute([]);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("Renumbered 1 ADR(s)", output);
        Assert.True(File.Exists(Path.Combine(dir.Path, "0000002-third.md")));
    }

    [Fact]
    public void RenumberCommand_Check_WithGap_ReturnsNonZeroAndDoesNotWrite()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First"]);
        new NewCommand(Service(dir.Path)).Execute(["Second"]);
        new NewCommand(Service(dir.Path)).Execute(["Third"]);
        File.Delete(Directory.GetFiles(dir.Path, "0000002-*.md")[0]); // gap: #1 and #3 remain

        Assert.Equal(1, new RenumberCommand(Service(dir.Path)).Execute(["--check"]));
        Assert.True(File.Exists(Path.Combine(dir.Path, "0000003-third.md")));
    }

    [Fact]
    public void InstallHooksCommand_NoGitRepo_Throws()
    {
        using var dir = new TempDirectory();

        Assert.Throws<AdrToolException>(() => new InstallHooksCommand(dir.Path).Execute([]));
    }

    [Fact]
    public void InstallHooksCommand_WritesExecutablePreCommitHook()
    {
        using var dir = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(dir.Path, ".git"));

        var output = CaptureOutput(() =>
        {
            var exitCode = new InstallHooksCommand(dir.Path).Execute([]);
            Assert.Equal(0, exitCode);
        });

        var hookPath = Path.Combine(dir.Path, ".git", "hooks", "pre-commit");
        Assert.Contains("Installed pre-commit hook", output);
        Assert.True(File.Exists(hookPath));
        Assert.Contains("adr lint", File.ReadAllText(hookPath));
        Assert.DoesNotContain("dashboard --check", File.ReadAllText(hookPath));
    }

    [Fact]
    public void InstallHooksCommand_WithDashboardCheck_IncludesDashboardCheck()
    {
        using var dir = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(dir.Path, ".git"));

        new InstallHooksCommand(dir.Path).Execute(["--dashboard-check"]);

        var hookPath = Path.Combine(dir.Path, ".git", "hooks", "pre-commit");
        Assert.Contains("adr dashboard --check", File.ReadAllText(hookPath));
    }

    [Fact]
    public void InstallHooksCommand_LocalToolManifest_UsesDotnetAdrInvocation()
    {
        using var dir = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(dir.Path, ".git"));
        Directory.CreateDirectory(Path.Combine(dir.Path, ".config"));
        File.WriteAllText(Path.Combine(dir.Path, ".config", "dotnet-tools.json"), "{}");

        new InstallHooksCommand(dir.Path).Execute([]);

        var hookPath = Path.Combine(dir.Path, ".git", "hooks", "pre-commit");
        Assert.Contains("dotnet adr lint", File.ReadAllText(hookPath));
    }

    [Fact]
    public void InstallHooksCommand_ExistingHookWithoutForce_Throws()
    {
        using var dir = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(dir.Path, ".git", "hooks"));
        File.WriteAllText(Path.Combine(dir.Path, ".git", "hooks", "pre-commit"), "#!/bin/sh\necho existing\n");

        Assert.Throws<AdrToolException>(() => new InstallHooksCommand(dir.Path).Execute([]));
        Assert.Contains("existing", File.ReadAllText(Path.Combine(dir.Path, ".git", "hooks", "pre-commit")));
    }

    [Fact]
    public void InstallHooksCommand_ExistingHookWithForce_Overwrites()
    {
        using var dir = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(dir.Path, ".git", "hooks"));
        File.WriteAllText(Path.Combine(dir.Path, ".git", "hooks", "pre-commit"), "#!/bin/sh\necho existing\n");

        new InstallHooksCommand(dir.Path).Execute(["--force"]);

        Assert.Contains("adr lint", File.ReadAllText(Path.Combine(dir.Path, ".git", "hooks", "pre-commit")));
    }

    [Fact]
    public void LintCommand_NoIssues_ReturnsZero()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First"]);

        var output = CaptureOutput(() =>
        {
            var exitCode = new LintCommand(Service(dir.Path)).Execute([]);
            Assert.Equal(0, exitCode);
        });

        Assert.Contains("No issues found.", output);
    }

    [Fact]
    public void LintCommand_MissingStatus_ReturnsNonZero()
    {
        using var dir = new TempDirectory();
        new NewCommand(Service(dir.Path)).Execute(["First"]);
        var file = Directory.GetFiles(dir.Path, "*.md")[0];
        File.WriteAllText(file, File.ReadAllText(file).Replace("- Status: Proposed", ""));

        var output = CaptureOutput(() =>
        {
            var exitCode = new LintCommand(Service(dir.Path)).Execute([]);
            Assert.Equal(1, exitCode);
        });

        Assert.Contains("Missing Status", output);
    }
}
