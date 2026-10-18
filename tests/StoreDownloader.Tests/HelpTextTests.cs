using StoreDownloader.Cli.Cli;
using Xunit;

namespace StoreDownloader.Tests;

public sealed class HelpTextTests
{
    public static TheoryData<string> Commands()
    {
        var data = new TheoryData<string>();
        foreach (var command in CommandCatalog.All)
        {
            data.Add(command.Name);
        }

        return data;
    }

    [Fact]
    public void IndexListsEveryCommandWithASummary()
    {
        using var writer = new StringWriter();

        HelpText.Print(writer);
        var text = writer.ToString();

        foreach (var command in CommandCatalog.All)
        {
            Assert.Contains(command.Name, text, StringComparison.Ordinal);
            Assert.Contains(command.Summary, text, StringComparison.Ordinal);
        }

        Assert.Contains("EXIT CODES", text, StringComparison.Ordinal);
        Assert.Contains("GLOBAL OPTIONS", text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void EveryCommandHasAManualPage(string name)
    {
        var command = CommandCatalog.Find(name);
        Assert.NotNull(command);

        using var writer = new StringWriter();
        HelpText.PrintCommand(command!, writer);
        var text = writer.ToString();

        Assert.Contains("SYNOPSIS", text, StringComparison.Ordinal);
        Assert.Contains($"storedl {command!.Name}", text, StringComparison.Ordinal);
        Assert.Contains("EXAMPLES", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(default: )", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandNamesAndSummariesAreUnique()
    {
        var names = CommandCatalog.All.Select(command => command.Name).ToList();
        var summaries = CommandCatalog.All.Select(command => command.Summary).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(summaries.Count, summaries.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void OptionsAreDocumentedWithTheirRealSpelling()
    {
        var install = CommandCatalog.Find("install");
        Assert.NotNull(install);

        var flags = install!.Options.Select(option => option.Flag).ToList();
        Assert.Contains("--dry-run", flags);
        Assert.Contains("--dir <path>", flags);
        Assert.Contains("--all-users", flags);
        Assert.All(install.Options, option => Assert.StartsWith("--", option.Flag, StringComparison.Ordinal));
    }

    [Fact]
    public void FindIsCaseInsensitiveAndRejectsUnknownNames()
    {
        Assert.NotNull(CommandCatalog.Find("INSTALL"));
        Assert.NotNull(CommandCatalog.Find(" doctor "));
        Assert.Null(CommandCatalog.Find("nonsense"));
        Assert.Null(CommandCatalog.Find(null));
    }

    [Fact]
    public void UnknownCommandOutputSuggestsCloseNames()
    {
        using var writer = new StringWriter();

        HelpText.PrintUnknownCommand("instal", writer);
        var text = writer.ToString();

        Assert.Contains("unknown command 'instal'", text, StringComparison.Ordinal);
        Assert.Contains("install", text, StringComparison.Ordinal);
    }
}
