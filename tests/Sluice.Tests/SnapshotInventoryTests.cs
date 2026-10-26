using Sluice.Windows;
using Xunit;

namespace Sluice.Tests;

public sealed class SnapshotInventoryTests
{
    private static InstalledPackage Package(string name, string version, bool framework = false) =>
        new(name, version, "x64", "Ok", $"{name}_{version}_x64__abcdefghij", framework);

    [Fact]
    public async Task ReadsTheMachineOnceForEveryQuestion()
    {
        // Planning asked three questions and started three PowerShell processes, each with its own view
        // of the machine.
        var reads = 0;
        var snapshot = new SnapshotInventory(_ =>
        {
            reads++;
            return Task.FromResult<IReadOnlyList<InstalledPackage>>(
            [
                Package("Microsoft.VCLibs.140.00", "14.0.33728.0", framework: true),
                Package("Contoso.Notes", "1.0.0.0"),
            ]);
        });

        await snapshot.GetFrameworksAsync();
        await snapshot.FindHighestAsync("Microsoft.VCLibs.140.00");
        await snapshot.FindHighestAsync("Contoso.Notes");

        Assert.Equal(1, reads);
        Assert.Equal(1, snapshot.Reads);
    }

    [Fact]
    public async Task KeepsFrameworksAndApplicationsApart()
    {
        var snapshot = new SnapshotInventory(_ => Task.FromResult<IReadOnlyList<InstalledPackage>>(
        [
            Package("Microsoft.VCLibs.140.00", "14.0.33728.0", framework: true),
            Package("Contoso.Notes", "1.0.0.0"),
        ]));

        var frameworks = await snapshot.GetFrameworksAsync();

        Assert.Single(frameworks);
        Assert.Equal("Microsoft.VCLibs.140.00", frameworks[0].Name);
        Assert.Null(await snapshot.FindHighestAsync("Microsoft.NotInstalled"));
    }

    [Fact]
    public async Task ReturnsTheHighestInstalledVersion()
    {
        var snapshot = new SnapshotInventory(_ => Task.FromResult<IReadOnlyList<InstalledPackage>>(
        [
            Package("Contoso.Notes", "1.0.0.0"),
            Package("Contoso.Notes", "3.0.0.0"),
            Package("Contoso.Notes", "2.0.0.0"),
        ]));

        var installed = await snapshot.FindHighestAsync("contoso.notes");

        Assert.NotNull(installed);
        Assert.Equal("3.0.0.0", installed!.Version);
    }
}
