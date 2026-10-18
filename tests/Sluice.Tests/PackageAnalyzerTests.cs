using Sluice.Packages;
using Sluice.Tests.Support;
using Xunit;

namespace Sluice.Tests;

public sealed class PackageAnalyzerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Sluice-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void ReadsAnApplicationManifest()
    {
        var path = PackageFixtureBuilder.CreateAppPackage(
            _directory,
            "Contoso.App_1.2.3.4_x64.msix",
            "Contoso.App",
            dependencies: [("Microsoft.VCLibs.140.00.UWPDesktop", "14.0.33728.0")]);

        var analysis = PackageAnalyzer.Analyze(path);

        Assert.Equal(PackageKind.App, analysis.Kind);
        Assert.NotNull(analysis.Identity);
        Assert.Equal("Contoso.App", analysis.Identity!.Name);
        Assert.Equal("1.2.3.4", analysis.Identity.Version);
        Assert.Equal("x64", analysis.Identity.Architecture);
        Assert.Equal("Windows.Desktop", analysis.TargetDeviceFamily);
        Assert.Equal("10.0.17763.0", analysis.MinimumWindowsVersion);
        Assert.Equal("Contoso Ltd", analysis.PublisherDisplayName);
        var dependency = Assert.Single(analysis.Dependencies);
        Assert.Equal("Microsoft.VCLibs.140.00.UWPDesktop", dependency.Name);
        Assert.Equal("14.0.33728.0", dependency.MinVersion);
    }

    [Fact]
    public void DetectsResourcePackages()
    {
        var path = PackageFixtureBuilder.CreateAppPackage(
            _directory,
            "Contoso.App_1.2.3.4_neutral_zh-CN.msix",
            "Contoso.App",
            architecture: "neutral",
            resourceId: "zh-CN",
            withApplications: false);

        var analysis = PackageAnalyzer.Analyze(path);

        Assert.Equal(PackageKind.Resource, analysis.Kind);
        Assert.Equal("zh-CN", analysis.Identity!.ResourceId);
    }

    [Fact]
    public void DetectsFrameworkPackages()
    {
        var path = PackageFixtureBuilder.CreateAppPackage(
            _directory,
            "Microsoft.VCLibs.140.00_x64.appx",
            "Microsoft.VCLibs.140.00",
            architecture: "x64",
            framework: true,
            withApplications: false,
            dependencies: []);

        var analysis = PackageAnalyzer.Analyze(path);

        Assert.Equal(PackageKind.Framework, analysis.Kind);
    }

    [Fact]
    public void ReadsBundleChildren()
    {
        var path = PackageFixtureBuilder.CreateBundle(
            _directory,
            "Contoso.App_1.2.3.4.msixbundle",
            "Contoso.App",
            "1.2.3.4",
            ("Contoso.App_1.2.3.4_x64.msix", "application", "x64", null),
            ("Contoso.App_1.2.3.4_arm64.msix", "application", "arm64", null),
            ("Contoso.App_1.2.3.4_x86.msix", "application", "x86", null),
            ("Contoso.App_1.2.3.4_neutral_zh-CN.msix", "resource", "neutral", "zh-CN"));

        var analysis = PackageAnalyzer.Analyze(path);

        Assert.Equal(PackageKind.Bundle, analysis.Kind);
        Assert.Equal(4, analysis.Children.Count);
        Assert.Equal(3, analysis.Children.Count(child => child.IsApplication));
        Assert.Single(analysis.Children, child => child.IsResource && child.ResourceId == "zh-CN");
    }

    [Fact]
    public void RejectsEncryptedPackagesWithAnExplicitMessage()
    {
        var path = PackageFixtureBuilder.CreateAppPackage(_directory, "Contoso.App_1.0.0.0_x64.eappx", "Contoso.App");

        var error = Assert.Throws<SluiceException>(() => PackageAnalyzer.Analyze(path));

        Assert.Contains("encrypted", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("license", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsZipFilesThatAreNotPackages()
    {
        var path = PackageFixtureBuilder.CreatePlainZip(_directory, "payload.zip");

        Assert.Throws<SluiceException>(() => PackageAnalyzer.Analyze(path));
        Assert.Null(PackageAnalyzer.TryAnalyze(path));
    }

    [Fact]
    public void AnalyzesBundlesByDefaultArchitectureAndLocale()
    {
        var path = PackageFixtureBuilder.CreateBundle(
            _directory,
            "Contoso.App_1.2.3.4.msixbundle",
            "Contoso.App",
            "1.2.3.4",
            ("Contoso.App_1.2.3.4_arm64.msix", "application", "arm64", null),
            ("Contoso.App_1.2.3.4_x64.msix", "application", "x64", null),
            ("Contoso.App_1.2.3.4_neutral.msix", "application", "neutral", null),
            ("Contoso.App_1.2.3.4_neutral_zh-CN.msix", "resource", "neutral", "zh-CN"),
            ("Contoso.App_1.2.3.4_neutral_fr-FR.msix", "resource", "neutral", "fr-FR"));

        var selection = BundlePackageSelector.Select(PackageAnalyzer.Analyze(path), "x64", ["zh-cn"]);

        Assert.NotNull(selection.Application);
        Assert.Equal("Contoso.App_1.2.3.4_x64.msix", selection.Application!.FileName);
        Assert.Single(selection.Resources);
        Assert.Equal("zh-CN", selection.Resources[0].ResourceId);
    }
}
