using Sluice.Install;
using Xunit;

namespace Sluice.Tests;

public sealed class RegisterFallbackTests
{
    [Fact]
    public void StagingLivesOutsideTheTempFolder()
    {
        // The registration points into the unpacked folder, so it has to survive: a temp cleaner that
        // deletes it silently breaks an app Windows still lists as installed.
        var root = AppxInstaller.RegisterStagingRoot;
        var temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        Assert.False(
            root.StartsWith(temp, StringComparison.OrdinalIgnoreCase),
            $"staging root '{root}' must not live under the temp folder '{temp}'");
        Assert.Contains("sluice", root, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AFailedAttemptTakesItsFilesBackOut()
    {
        // Every retry used to leave a fresh sluice-<guid> folder of a few hundred megabytes behind,
        // because nothing removed it once the registration had failed.
        var script = AppxInstaller.BuildRegisterScript(@"C:\packages\Controller.msixbundle");

        Assert.Contains(AppxInstaller.RegisterStagingRoot, script, StringComparison.Ordinal);
        Assert.Contains("Remove-Item -LiteralPath $destination -Recurse -Force", script, StringComparison.Ordinal);
        Assert.Contains("Add-AppxPackage -Register", script, StringComparison.Ordinal);
    }
}
