using Sluice.Cli;
using Sluice.Tests.Support;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// The manual lists <c>-v</c> and <c>--version</c> as global options. They were only honoured as the first
/// argument, so <c>Sluice install --version</c> answered that install needs a Store reference - and
/// <c>Sluice install &lt;id&gt; --version</c> would have gone ahead and installed.
/// </summary>
public sealed class CliEntryPointTests
{
    [Theory]
    [InlineData("install")]
    [InlineData("download")]
    [InlineData("plan")]
    [InlineData("doctor")]
    public async Task TheVersionSwitchWorksAfterACommandName(string command)
    {
        var run = await CliRunner.RunAsync(() => Program.Main([command, "--version"]));

        Assert.Equal(0, run.ExitCode);
        Assert.Contains(BuildInfo.Version, run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheVersionSwitchWinsOverAStoreReference()
    {
        // Nothing may be resolved or installed when the operator asked for the version.
        var run = await CliRunner.RunAsync(() => Program.Main(["install", "9NKSQGP7F2NH", "--version"]));

        Assert.Equal(0, run.ExitCode);
        Assert.Contains(BuildInfo.Version, run.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Product", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownCommandStillFails()
    {
        var run = await CliRunner.RunAsync(() => Program.Main(["frobnicate"]));

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("unknown command", run.StandardError, StringComparison.Ordinal);
    }
}
