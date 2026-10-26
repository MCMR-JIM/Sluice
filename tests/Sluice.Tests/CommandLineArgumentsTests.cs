using Sluice.Cli.Cli;
using Xunit;

namespace Sluice.Tests;

public sealed class CommandLineArgumentsTests
{
    [Fact]
    public void ParsesOptionsWithValuesAndBooleanSwitches()
    {
        var arguments = CommandLineArguments.Parse(["--dir", @"D:\cache", "--json", "9NKSQGP7F2NH"]);

        Assert.Equal(@"D:\cache", arguments.Get("dir"));
        Assert.True(arguments.Has("json"));
        Assert.Null(arguments.Get("json"));
        Assert.Equal(["9NKSQGP7F2NH"], arguments.Positionals);
    }

    [Fact]
    public void BooleanSwitchesDoNotConsumePositionals()
    {
        var arguments = CommandLineArguments.Parse(["--json", "9NKSQGP7F2NH", "--locale", "zh-cn"]);

        Assert.True(arguments.Has("json"));
        Assert.Equal("zh-cn", arguments.Get("locale"));
        Assert.Equal(["9NKSQGP7F2NH"], arguments.Positionals);
    }

    [Fact]
    public void DoesNotSwallowFollowingOptions()
    {
        var arguments = CommandLineArguments.Parse(["--yes", "--arch", "x64"]);

        Assert.True(arguments.Has("yes"));
        Assert.Equal("x64", arguments.Get("arch"));
        Assert.Empty(arguments.Positionals);
    }

    [Fact]
    public void SupportsEqualsSyntax()
    {
        var arguments = CommandLineArguments.Parse(["--locale=zh-cn", "--dir=C:\\path with space"]);

        Assert.Equal("zh-cn", arguments.Get("locale"));
        Assert.Equal(@"C:\path with space", arguments.Get("dir"));
    }

    [Fact]
    public void TreatsEverythingAfterDoubleDashAsPositional()
    {
        var arguments = CommandLineArguments.Parse(["--json", "--", "--not-an-option"]);

        Assert.Equal(["--not-an-option"], arguments.Positionals);
    }

    [Fact]
    public void ReportsOptionsTheCommandDoesNotTake()
    {
        // "sluice download <url> --al-arch" used to be accepted and ignored, so the command ran with
        // its defaults while the caller believed every architecture had been kept.
        var arguments = CommandLineArguments.Parse(["--al-arch", "--dir", "cache", "--json"]);

        var unaccepted = arguments.FindUnaccepted(["dir", "json", "arch"]);

        Assert.Equal(["al-arch"], unaccepted);
    }

    [Fact]
    public void AcceptsEveryOptionItKnowsInAnyCasing()
    {
        var arguments = CommandLineArguments.Parse(["--JSON", "--Dir", "cache"]);

        Assert.Empty(arguments.FindUnaccepted(["json", "dir"]));
    }

    [Fact]
    public void ParsesIntegersWithFallbacks()
    {
        var arguments = CommandLineArguments.Parse(["--parallel", "8"]);

        Assert.Equal(8, arguments.GetInt32("parallel", 3));
        Assert.Equal(3, arguments.GetInt32("missing", 3));
    }
}
