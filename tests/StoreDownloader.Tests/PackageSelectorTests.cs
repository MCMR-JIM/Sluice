using StoreDownloader.Fe3;
using StoreDownloader.Packages;
using StoreDownloader.Resolution;
using StoreDownloader.Store;
using Xunit;

namespace StoreDownloader.Tests;

public sealed class PackageSelectorTests
{
    private const string FamilyName = "Contoso.Notes_abcdefghij";

    [Fact]
    public void PicksTheNewestMainBundleAndCompatibleDependencies()
    {
        var resolution = CreateResolution(
            Package("Contoso.Notes", "1.0.0.0", "neutral", 100),
            Package("Contoso.Notes", "2.0.0.0", "neutral", 200),
            Package("Microsoft.VCLibs.140.00", "14.0.30035.0", "x64", 50),
            Package("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64", 60),
            Package("Microsoft.VCLibs.140.00", "14.0.33728.0", "arm64", 70),
            Package("Microsoft.WindowsAppRuntime.2", "2.5.1.0", "x64", 80),
            Package("Microsoft.WindowsAppRuntime.2", "2.5.1.0", "arm64", 90));

        var selection = PackageSelector.Select(resolution, new PackageSelectionOptions("x64", "en-US"));

        var main = Assert.Single(selection.Main);
        Assert.Equal("Contoso.Notes_2.0.0.0_neutral.msixbundle", main.Package.ReadableFileName);

        Assert.Equal(2, selection.Dependencies.Count);
        var libs = selection.Dependencies.Single(package => package.Package.PackageIdentityName == "Microsoft.VCLibs.140.00");
        Assert.Equal("14.0.33728.0", libs.Package.Version);
        Assert.Equal("x64", libs.Package.Architecture);
        var runtime = selection.Dependencies.Single(package => package.Package.PackageIdentityName == "Microsoft.WindowsAppRuntime.2");
        Assert.Equal("x64", runtime.Package.Architecture);

        Assert.Contains(selection.Skipped, note => note.Contains("arm64", StringComparison.Ordinal));
    }

    [Fact]
    public void PrefersTheNativeArchitectureOverNeutral()
    {
        var resolution = CreateResolution(
            Package("Contoso.Notes", "2.0.0.0", "neutral", 200),
            Package("Contoso.Notes", "2.0.0.0", "x64", 180));

        var selection = PackageSelector.Select(resolution, new PackageSelectionOptions("x64", "en-US"));

        Assert.Equal("x64", Assert.Single(selection.Main).Package.Architecture);
    }

    [Fact]
    public void KeepsResourcePackagesForTheRequestedLocale()
    {
        var resolution = CreateResolution(
            Package("Contoso.Notes", "2.0.0.0", "neutral", 200),
            Package("Contoso.Notes", "2.0.0.0", "neutral", 20, resourceId: "zh-CN"),
            Package("Contoso.Notes", "2.0.0.0", "neutral", 21, resourceId: "fr-FR"));

        var selection = PackageSelector.Select(resolution, new PackageSelectionOptions("x64", "zh-cn"));

        var resource = Assert.Single(selection.Resources);
        Assert.Equal("zh-CN", resource.Package.InstallerSpecificIdentifier.Split('_')[3]);
    }

    [Fact]
    public void CanKeepEveryArchitectureForOfflineArchives()
    {
        var resolution = CreateResolution(
            Package("Contoso.Notes", "2.0.0.0", "neutral", 200),
            Package("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64", 60),
            Package("Microsoft.VCLibs.140.00", "14.0.33728.0", "arm64", 70));

        var selection = PackageSelector.Select(
            resolution,
            new PackageSelectionOptions("x64", "en-US", IncludeAllArchitectures: true));

        Assert.Equal(2, selection.Dependencies.Count);
        Assert.Empty(selection.Skipped);
    }

    [Fact]
    public void SkipsPackagesWithoutADownloadUrl()
    {
        var resolution = CreateResolution(
            Package("Contoso.Notes", "2.0.0.0", "neutral", 200),
            Package("Microsoft.VCLibs.140.00", "14.0.33728.0", "x64", 60, downloadable: false));

        var selection = PackageSelector.Select(resolution, new PackageSelectionOptions("x64", "en-US"));

        Assert.Single(selection.Main);
        Assert.Empty(selection.Dependencies);
        Assert.Contains(selection.Skipped, note => note.Contains("no download url", StringComparison.Ordinal));
    }

    private static PackageResolution CreateResolution(params ResolvedPackage[] packages) =>
        new(
            new StoreProduct(
                "9TESTTEST01",
                "Contoso Notes",
                "Contoso",
                FamilyName,
                [new StorePackageGroup("11111111-1111-1111-1111-111111111111", "MsixBundle", FamilyName, ["x64"])],
                "US",
                "en-US",
                "displaycatalog"),
            packages);

    private static ResolvedPackage Package(
        string identityName,
        string version,
        string architecture,
        long size,
        string? resourceId = null,
        bool downloadable = true)
    {
        var extension = identityName == "Contoso.Notes" ? ".msixbundle" : ".appx";
        var identity = resourceId is null
            ? $"{identityName}_{version}_{architecture}__abcdefghij"
            : $"{identityName}_{version}_{architecture}_{resourceId}__abcdefghij";
        var readable = resourceId is null
            ? $"{identityName}_{version}_{architecture}{extension}"
            : $"{identityName}_{version}_{architecture}_{resourceId}{extension}";

        var package = new WuPackage(
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString(),
            1,
            identityName,
            Guid.NewGuid() + extension,
            readable,
            size,
            "sha1-" + readable,
            "sha256-" + readable,
            identity,
            architecture,
            version,
            false,
            identityName != "Contoso.Notes",
            true);

        return new ResolvedPackage(package, downloadable ? "https://tlu.dl.example/" + readable : null);
    }
}
