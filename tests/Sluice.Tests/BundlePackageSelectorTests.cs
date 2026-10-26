using Sluice.Packages;
using Sluice.Tests.Support;
using Xunit;

namespace Sluice.Tests;

public sealed class BundlePackageSelectorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Sluice-bundle-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void RefusesToSelectAChildBuiltForAnotherArchitecture()
    {
        // MatchScore alone sorted the children and then took the first one, so a bundle without a
        // package for the target architecture still returned the biggest foreign one. The planner's
        // "bundle has no application package for this architecture" error could never fire, and the
        // install failed later with 0x80073CFD instead.
        var bundle = PackageFixtureBuilder.CreateBundle(
            _directory,
            "Contoso.Notes_2.0.0.0.msixbundle",
            "Contoso.Notes",
            "2.0.0.0",
            ("Contoso.Notes_2.0.0.0_arm64.msix", "application", "arm64", null),
            ("Contoso.Notes_2.0.0.0_arm64_x64.msix", "application", "x64", null));

        var analysis = PackageAnalyzer.Analyze(bundle);

        var selection = BundlePackageSelector.Select(analysis, "arm64");

        Assert.NotNull(selection.Application);
        Assert.Equal("arm64", selection.Application!.Architecture);
    }

    [Fact]
    public void ReturnsNoApplicationWhenTheBundleHasNothingForTheTarget()
    {
        var bundle = PackageFixtureBuilder.CreateBundle(
            _directory,
            "Contoso.Notes_2.0.0.0.msixbundle",
            "Contoso.Notes",
            "2.0.0.0",
            ("Contoso.Notes_2.0.0.0_arm64.msix", "application", "arm64", null));

        var analysis = PackageAnalyzer.Analyze(bundle);

        var selection = BundlePackageSelector.Select(analysis, "x64");

        Assert.Null(selection.Application);
        Assert.Contains(selection.Notes, note => note.Code == "BundleNoMatchingArchitecture");
    }

    [Fact]
    public void KeepsANeutralApplicationForAnyTarget()
    {
        var bundle = PackageFixtureBuilder.CreateBundle(
            _directory,
            "Contoso.Notes_2.0.0.0.msixbundle",
            "Contoso.Notes",
            "2.0.0.0",
            ("Contoso.Notes_2.0.0.0_neutral.msix", "application", "neutral", null));

        var analysis = PackageAnalyzer.Analyze(bundle);

        Assert.NotNull(BundlePackageSelector.Select(analysis, "x64").Application);
    }
}
