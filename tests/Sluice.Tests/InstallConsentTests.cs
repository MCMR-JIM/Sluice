using Sluice.Cli.Commands;
using Xunit;

namespace Sluice.Tests;

public sealed class InstallConsentTests
{
    [Fact]
    public void AYesSwitchInstallsWithoutAsking()
    {
        var asked = false;
        var decision = InstallCommand.DecideConsent(assumeYes: true, stdinRedirected: true, () => { asked = true; return false; });

        Assert.True(decision.Proceed);
        Assert.False(asked);
    }

    [Fact]
    public void APipedRunWithoutAYesSwitchIsRefusedInsteadOfInstalling()
    {
        // Standard input being a pipe means nobody can answer the question, and the old code skipped
        // it and installed anyway: a piped run deployed packages without consent.
        var asked = false;
        var decision = InstallCommand.DecideConsent(assumeYes: false, stdinRedirected: true, () => { asked = true; return true; });

        Assert.False(decision.Proceed);
        Assert.False(asked);
        Assert.Equal(2, decision.ExitCode);
        Assert.Contains("--yes", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATerminalPromptDecides()
    {
        Assert.True(InstallCommand.DecideConsent(assumeYes: false, stdinRedirected: false, () => true).Proceed);

        var refused = InstallCommand.DecideConsent(assumeYes: false, stdinRedirected: false, () => false);
        Assert.False(refused.Proceed);
        Assert.Equal(1, refused.ExitCode);
        Assert.Null(refused.Message);
    }
}
