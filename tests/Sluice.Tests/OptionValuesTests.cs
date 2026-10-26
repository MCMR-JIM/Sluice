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
}
