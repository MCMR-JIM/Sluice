using Sluice.Cli.Cli;
using Xunit;

namespace Sluice.Tests;

public sealed class OptionValuesTests
{
    [Theory]
    [InlineData("x64", "x64")]
    [InlineData("X64", "x64")]
    [InlineData("amd64", "x64")]
    [InlineData("aarch64", "arm64")]
    [InlineData(" arm ", "arm")]
    public void AcceptsEveryShapedArchitecture(string typed, string expected)
    {
        var arguments = CommandLineArguments.Parse(["--arch", typed]);

        Assert.Equal(expected, OptionValues.Architecture(arguments));
    }

    [Fact]
    public void RejectsAnArchitectureThatDoesNotExist()
    {
        // A misspelled value used to travel into the selection, match nothing, and be reported as a
        // product problem instead of a typing problem.
        var arguments = CommandLineArguments.Parse(["--arch", "x46"]);

        var error = Assert.Throws<FormatException>(() => OptionValues.Architecture(arguments));
        Assert.Contains("x46", error.Message, StringComparison.Ordinal);
        Assert.Contains("x64", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultsToThisMachine()
    {
        var arguments = CommandLineArguments.Parse(["9NKSQGP7F2NH"]);

        Assert.Equal(Sluice.Packages.WindowsArchitecture.Current, OptionValues.Architecture(arguments));
    }

    [Theory]
    [InlineData("zh_CN", "zh-CN")]
    [InlineData("ZH_cn", "ZH-cn")]
    [InlineData(" zh-Hans-CN ", "zh-Hans-CN")]
    [InlineData("en", "en")]
    public void NormalisesTheSeparatorPeopleActuallyType(string typed, string expected)
    {
        var arguments = CommandLineArguments.Parse(["--locale", typed]);

        Assert.Equal(expected, OptionValues.Locale(arguments));
    }

    [Fact]
    public void RejectsSomethingThatIsNotALanguageTag()
    {
        // A tag that is not a tag used to travel into the delivery request, which answered with a fault
        // that read like an outage.
        var arguments = CommandLineArguments.Parse(["--locale", "chinese"]);

        var error = Assert.Throws<FormatException>(() => OptionValues.Locale(arguments));
        Assert.Contains("chinese", error.Message, StringComparison.Ordinal);
        Assert.Contains("zh-CN", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a&b")]
    [InlineData("zh CN")]
    [InlineData("zh-CN-")]
    [InlineData("-zh")]
    public void RejectsAnUnusableLocale(string typed)
    {
        var arguments = CommandLineArguments.Parse(["--locale", typed]);

        Assert.Throws<FormatException>(() => OptionValues.Locale(arguments));
    }

    [Fact]
    public void FallsBackToTheDisplayLanguage()
    {
        var arguments = CommandLineArguments.Parse(["9NKSQGP7F2NH"]);

        Assert.Equal(Sluice.Cli.Cli.Defaults.Locale, OptionValues.Locale(arguments));
    }
}
