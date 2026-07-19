using AdrTool.Commands;
using Xunit;

namespace AdrTool.Tests;

public class CommandArgsTests
{
    [Fact]
    public void Parse_SeparatesKeyValueArgsFromTitleWords()
    {
        var (title, args) = CommandArgs.Parse(["Use", "--author=Jane", "clean", "--Team=Platform", "architecture"]);

        Assert.Equal("Use clean architecture", title);
        Assert.Equal("Jane", args["author"]);
        Assert.Equal("Platform", args["Team"]);
    }

    [Fact]
    public void Parse_KeyLookupIsCaseInsensitive()
    {
        var (_, args) = CommandArgs.Parse(["--Author=Jane"]);

        Assert.True(args.TryGetValue("author", out var value));
        Assert.Equal("Jane", value);
    }

    [Fact]
    public void Parse_WithNoCustomArgs_ReturnsEmptyDictionary()
    {
        var (title, args) = CommandArgs.Parse(["Just", "a", "title"]);

        Assert.Equal("Just a title", title);
        Assert.Empty(args);
    }

    [Fact]
    public void Parse_DoubleDashWithoutEquals_IsTreatedAsTitleWord()
    {
        var (title, args) = CommandArgs.Parse(["--no-number", "Title"]);

        Assert.Equal("--no-number Title", title);
        Assert.Empty(args);
    }

    [Fact]
    public void ParseTags_SplitsAndTrimsCommaSeparatedList()
    {
        var (_, args) = CommandArgs.Parse(["--tags=security, infra ,security"]);

        var tags = CommandArgs.ParseTags(args);

        Assert.Equal(["security", "infra", "security"], tags);
    }

    [Fact]
    public void ParseTags_WithNoTagsArg_ReturnsEmpty()
    {
        var (_, args) = CommandArgs.Parse(["Title"]);

        Assert.Empty(CommandArgs.ParseTags(args));
    }
}
