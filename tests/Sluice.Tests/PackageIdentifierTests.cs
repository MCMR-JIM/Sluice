using Sluice.Fe3;
using Xunit;

namespace Sluice.Tests;

public sealed class PackageIdentifierTests
{
    [Fact]
    public void ParsesApplicationPackage()
    {
        Assert.True(PackageIdentifier.TryParse("Contoso.Notes_1.2.3.4_x64__abcdefghij", out var identifier));

        Assert.Equal("Contoso.Notes", identifier.Name);
        Assert.Equal("1.2.3.4", identifier.Version);
        Assert.Equal("x64", identifier.Architecture);
        Assert.Null(identifier.ResourceId);
        Assert.Equal("abcdefghij", identifier.PublisherId);
    }

    [Fact]
    public void ParsesBundleIdentifierWithEmptyResourceId()
    {
        Assert.True(PackageIdentifier.TryParse("5319275A.WhatsAppDesktop_2.2640.100.0_neutral_~_cv1g1gvanyjgm", out var identifier));

        Assert.Equal("5319275A.WhatsAppDesktop", identifier.Name);
        Assert.Equal("2.2640.100.0", identifier.Version);
        Assert.Equal("neutral", identifier.Architecture);
        Assert.Null(identifier.ResourceId);
        Assert.Equal("cv1g1gvanyjgm", identifier.PublisherId);
    }

    [Fact]
    public void ParsesResourcePackage()
    {
        Assert.True(PackageIdentifier.TryParse("Contoso.Notes_1.2.3.4_x64_zh-CN__abcdefghij", out var identifier));

        Assert.Equal("zh-CN", identifier.ResourceId);
        Assert.Equal("x64", identifier.Architecture);
    }

    [Fact]
    public void KeepsUnderscoresInsidePackageNames()
    {
        Assert.True(PackageIdentifier.TryParse("Microsoft.VCLibs.140.00.UWPDesktop_14.0.33728.0_arm64__8wekyb3d8bbwe", out var identifier));

        Assert.Equal("Microsoft.VCLibs.140.00.UWPDesktop", identifier.Name);
        Assert.Equal("arm64", identifier.Architecture);
        Assert.Equal("8wekyb3d8bbwe", identifier.PublisherId);
    }

    [Fact]
    public void FallsBackToWholeNameWhenNoVersionIsPresent()
    {
        Assert.True(PackageIdentifier.TryParse("Contoso.Notes", out var identifier));

        Assert.Equal("Contoso.Notes", identifier.Name);
        Assert.Null(identifier.Version);
        Assert.Null(identifier.Architecture);
    }

    [Fact]
    public void RejectsEmptyIdentifiers()
    {
        Assert.False(PackageIdentifier.TryParse(null, out _));
        Assert.False(PackageIdentifier.TryParse("   ", out _));
    }

    [Theory]
    [InlineData("1.2.3.4", "1.2.3.4")]
    [InlineData("2.2640.0.0", "2.2640.0.0")]
    [InlineData("14.0.33728.0", "14.0.33728.0")]
    public void ExposesComparableVersions(string raw, string expected)
    {
        Assert.True(PackageIdentifier.TryParse($"Contoso.App_{raw}_x64__abcdefghij", out var identifier));
        Assert.Equal(Version.Parse(expected), identifier.ToVersion());
    }
}
