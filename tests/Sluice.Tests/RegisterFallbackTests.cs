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
    public void ABundleIsNotOfferedTheFallbackItCannotUse()
    {
        // The fallback unpacks the file and looks for an AppxManifest.xml. A bundle holds a bundle
        // manifest and child packages, so the attempt unpacked hundreds of megabytes, failed, and
        // replaced the Windows error with its own "No AppxManifest.xml found after extraction".
        Assert.False(AppxInstaller.CanRegisterLooseFiles(@"C:packagesController.msixbundle"));
        Assert.False(AppxInstaller.CanRegisterLooseFiles(@"C:packagesController.appxbundle"));
        Assert.True(AppxInstaller.CanRegisterLooseFiles(@"C:packages\Controller_1.0.0.0_x64.msix"));
        Assert.True(AppxInstaller.CanRegisterLooseFiles(@"C:packages\Controller_1.0.0.0_x64.appx"));
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
