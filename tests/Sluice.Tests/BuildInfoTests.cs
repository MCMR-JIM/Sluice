using Sluice.Cli.Cli;
using Xunit;

namespace Sluice.Tests;

public sealed class BuildInfoTests
{
    [Fact]
    public void ExposesTheToolNameAndVersion()
    {
        Assert.Equal("Sluice", BuildInfo.ToolName);
        Assert.False(string.IsNullOrWhiteSpace(BuildInfo.Version));
        Assert.Matches(@"^\d+\.\d+\.\d+", BuildInfo.Version);
    }

    [Fact]
    public void HelpMentionsEveryCommand()
    {
        using var writer = new StringWriter();

        HelpText.Print(writer);
        var help = writer.ToString();

        foreach (var command in new[] { "install", "resolve", "download", "analyze", "plan", "doctor", "explain" })
        {
            Assert.Contains(command, help, StringComparison.Ordinal);
        }

        Assert.Contains("--version", help, StringComparison.Ordinal);
    }
}
