using Sluice.Fe3;
using Sluice.Packages;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// A dry run names the role of every package it would fetch. It answered "framework or main", so a
/// Chinese resource package - which is neither - was printed as the application itself.
/// </summary>
public sealed class PackageRoleTests
{
    [Fact]
    public void AResourcePackageIsNotLabelledAsTheApplication()
    {
        Assert.Equal("resource", PackageRole.Of(Package("Contoso.Notes_1.0.0.0_neutral_zh-CN_abcdefghij")));
    }

    [Fact]
    public void AFrameworkIsAFramework()
    {
        Assert.Equal("framework", PackageRole.Of(Package("Microsoft.VCLibs.140.00_14.0.30704.0_x64__8wekyb3d8bbwe", framework: true)));
    }

    [Fact]
    public void TheApplicationIsMain()
    {
        Assert.Equal("main", PackageRole.Of(Package("Contoso.Notes_1.0.0.0_x64__abcdefghij")));
    }

    private static WuPackage Package(string identifier, bool framework = false) =>
        new(
            "1",
            Guid.NewGuid().ToString(),
            1,
            "Contoso.Notes",
            identifier + ".appx",
            identifier + ".appx",
            1024,
            null,
            null,
            identifier,
            null,
            null,
            false,
            framework,
            true);
}
