using Sluice.Fe3;
using Sluice.Install;
using Sluice.Packages;
using Sluice.Resolution;
using Xunit;

namespace Sluice.Tests;

public sealed class PackagePreparerTests
{
    [Fact]
    public void DownloadsOnlyTheDependenciesTheManifestNames()
    {
        var selection = new PackageSelection(
            [Create("Contoso.Notes", "2.0.0.0", "neutral", 1024, ".msixbundle")],
            [
                Create("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64", 512, ".appx"),
                Create("Microsoft.WindowsAppRuntime.2", "2.5.1.0", "x64", 900, ".msix"),
                Create("Microsoft.UI.Xaml.2.7", "7000.785.2325.0", "x64", 700, ".appx"),
            ],
            [],
            []);

        var required = PackagePreparer.SelectRequired(
            selection,
            [new PackageDependency("Microsoft.VCLibs.140.00", "14.0.30035.0", null)],
            "en-US");

        var only = Assert.Single(required);
        Assert.Equal("Microsoft.VCLibs.140.00", only.Package.PackageIdentityName);
    }

    [Fact]
    public void ExplainsWhatItLeftOut()
    {
        var selection = new PackageSelection(
            [Create("Contoso.Notes", "2.0.0.0", "neutral", 1024, ".msixbundle")],
            [Create("Microsoft.UI.Xaml.2.7", "7000.785.2325.0", "x64", 700, ".appx")],
            [],
            []);
        var notes = new List<Sluice.Diagnostics.DiagnosticMessage>();

        PackagePreparer.SelectRequired(selection, [], "en-US", notes);

        Assert.Contains(notes, note => note.Text.Contains("Microsoft.UI.Xaml.2.7", StringComparison.Ordinal)
            && note.Text.Contains("not declared", StringComparison.Ordinal));
    }

    [Fact]
    public void KeepsResourcesThatBelongToTheApplication()
    {
        var selection = new PackageSelection(
            [Create("Contoso.Notes", "2.0.0.0", "neutral", 1024, ".msixbundle")],
            [],
            [
                Create("Contoso.Notes", "2.0.0.0", "neutral", 64, ".appx", "zh-CN"),
                Create("Microsoft.WindowsAppRuntime.2", "2.5.1.0", "neutral", 64, ".appx", "zh-CN"),
            ],
            []);

        var required = PackagePreparer.SelectRequired(selection, [], "zh-cn");

        var only = Assert.Single(required);
        Assert.Equal("Contoso.Notes", only.Package.PackageIdentityName);
        Assert.Contains("zh-CN", only.Package.InstallerSpecificIdentifier, StringComparison.Ordinal);
    }

    [Fact]
    public void CodesTheNoteAboutAForeignResourcePackage()
    {
        // This note was written as a finished English sentence, so the window could not translate it while
        // every other note followed the language.
        var selection = new PackageSelection(
            [Create("Contoso.Notes", "2.0.0.0", "neutral", 1024, ".msixbundle")],
            [],
            [Create("Microsoft.WindowsAppRuntime.2", "2.5.1.0", "neutral", 64, ".appx", "zh-CN")],
            []);
        var notes = new List<Sluice.Diagnostics.DiagnosticMessage>();

        var required = PackagePreparer.SelectRequired(selection, [], "zh-cn", notes);

        Assert.Empty(required);
        Assert.Contains(notes, note => note.Code == "PrepareForeignResource");
    }

    [Fact]
    public void NeverDownloadsAFrameworkTheManifestDidNotAskFor()
    {
        var selection = new PackageSelection(
            [Create("Contoso.Notes", "2.0.0.0", "neutral", 1024, ".msixbundle")],
            [
                Create("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64", 512, ".appx"),
                Create("Microsoft.VCLibs.140.00", "14.0.33728.0", "arm64", 512, ".appx"),
            ],
            [],
            []);

        var required = PackagePreparer.SelectRequired(
            selection,
            [new PackageDependency("Microsoft.VCLibs.140.00", "14.0.30035.0", null), new PackageDependency("Contoso.Framework", "1.0.0.0", null)],
            "en-US");

        Assert.Equal(2, required.Count);
    }

    private static ResolvedPackage Create(string name, string version, string architecture, long size, string extension, string? resourceId = null)
    {
        var identity = resourceId is null
            ? $"{name}_{version}_{architecture}__abcdefghij"
            : $"{name}_{version}_{architecture}_{resourceId}__abcdefghij";
        var readable = resourceId is null
            ? $"{name}_{version}_{architecture}{extension}"
            : $"{name}_{version}_{architecture}_{resourceId}{extension}";

        var package = new WuPackage(
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString(),
            1,
            name,
            Guid.NewGuid() + extension,
            readable,
            size,
            "sha1",
            "sha256",
            identity,
            architecture,
            version,
            false,
            false,
            true);

        return new ResolvedPackage(package, "https://example.invalid/" + readable);
    }
}
