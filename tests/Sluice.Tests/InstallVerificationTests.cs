using Sluice.Install;
using Sluice.Windows;
using Xunit;

namespace Sluice.Tests;

public sealed class InstallVerificationTests
{
    private static InstalledPackage Installed(string version) =>
        new("Contoso.Notes", version, "x64", "Ok", $"Contoso.Notes_{version}_x64__abcdefghij", false);

    [Fact]
    public void ACheckThatCouldNotRunIsNotAFailedInstall()
    {
        // Reading the inventory can fail on its own (PowerShell unavailable, a wedged deployment
        // service). Reporting that as "the install did not finish" plus "nothing is registered" sent
        // people looking for a package Windows had just installed.
        var (succeeded, problem) = AppxInstaller.EvaluateVerification(null, "2.0.0.0", "Get-AppxPackage failed");

        Assert.True(succeeded);
        Assert.Equal("Get-AppxPackage failed", problem);
    }

    [Fact]
    public void AMissingRegistrationIsAFailedInstall()
    {
        var (succeeded, problem) = AppxInstaller.EvaluateVerification(null, "2.0.0.0", null);

        Assert.False(succeeded);
        Assert.Null(problem);
    }

    [Fact]
    public void AnOlderRegistrationIsAFailedInstall()
    {
        var (succeeded, _) = AppxInstaller.EvaluateVerification(Installed("1.0.0.0"), "2.0.0.0", null);

        Assert.False(succeeded);
    }

    [Fact]
    public void ARegistrationAtOrAboveThePlannedVersionPasses()
    {
        Assert.True(AppxInstaller.EvaluateVerification(Installed("2.0.0.0"), "2.0.0.0", null).Succeeded);
        Assert.True(AppxInstaller.EvaluateVerification(Installed("2.5.0.0"), "2.0.0.0", null).Succeeded);
    }

    [Theory]
    [InlineData(true, null, true, true)]
    [InlineData(true, "Get-AppxPackage failed", true, false)]
    [InlineData(false, null, true, false)]
    [InlineData(true, null, false, false)]
    public void AVerifiedInstallIsOneThatWasActuallyChecked(bool succeeded, string? problem, bool requested, bool expected)
    {
        // "--no-verify" reads nothing back, so the outcome carries neither a registration nor a problem, and
        // "succeeded and no problem" was printed as verified: a checked install and an unchecked one were
        // indistinguishable.
        Assert.Equal(expected, InstallVerdict.Verified(succeeded, problem, requested));
    }

    [Theory]
    [InlineData(false, null, "skipped")]
    [InlineData(true, null, "checked")]
    [InlineData(true, "boom", "unverified")]
    public void TheCheckHasThreeAnswersNotTwo(bool requested, string? problem, string expected)
    {
        Assert.Equal(expected, InstallVerdict.Verification(requested, problem));
    }

    [Fact]
    public void TheMessageDoesNotClaimThePackageIsMissingWhenTheCheckCouldNotRun()
    {
        var message = AppxInstaller.DescribeRegistration(null, "Contoso.Notes", "Get-AppxPackage failed");

        Assert.Contains("could not be read back", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Nothing is registered", message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMessageSaysWhatIsRegisteredWhenTheCheckRan()
    {
        var message = AppxInstaller.DescribeRegistration(Installed("2.0.0.0"), "Contoso.Notes", null);

        Assert.Contains("is registered with status Ok", message, StringComparison.Ordinal);
    }
}
