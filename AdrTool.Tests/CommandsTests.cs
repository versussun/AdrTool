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
}
