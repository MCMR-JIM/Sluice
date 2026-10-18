using Sluice.Fe3;
using Xunit;

namespace Sluice.Tests;

public sealed class SyncUpdatesParserTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "sync-updates-sample.xml");

    private static IReadOnlyList<WuPackage> ParseFixture() =>
        SyncUpdatesParser.Parse(File.ReadAllText(FixturePath));

    [Fact]
    public void ParsesPackagesAndSkipsBlockMapSideFiles()
    {
        var packages = ParseFixture();

        // Two real packages; the dynamic metadata cab and the duplicate update are not packages.
        Assert.Equal(2, packages.Count);
        Assert.DoesNotContain(packages, package => package.Extension == ".cab");
    }

    [Fact]
    public void ReadsIdentityDigestsAndFlags()
    {
        var package = ParseFixture().Single(candidate => candidate.PackageIdentityName == "Contoso.Notes");

        Assert.Equal("11111111-1111-1111-1111-111111111111", package.UpdateId);
        Assert.Equal(1, package.RevisionNumber);
        Assert.Equal("c0ffee00-0000-4000-8000-000000000001.appx", package.DeliveryFileName);
        Assert.Equal("Contoso.Notes_1.2.3.4_x64.appx", package.ReadableFileName);
        Assert.Equal("x64", package.Architecture);
        Assert.Equal("1.2.3.4", package.Version);
        Assert.Equal(4096, package.SizeInBytes);
        Assert.Equal("AAAAAAAAAAAAAAAAAAAAAAAAAAA=", package.Sha1Digest);
        Assert.Equal("BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB=", package.Sha256Digest);
        Assert.True(package.IsMainPackage);
        Assert.False(package.IsFramework);
        Assert.True(package.HasFileUrl);
        Assert.False(package.IsBundle);
        Assert.True(package.IsSinglePackage);
    }

    [Fact]
    public void PrefersTheCopyThatHasADownloadUrlWhenDuplicated()
    {
        var packages = ParseFixture();
        var framework = packages.Single(package => package.PackageIdentityName == "Microsoft.VCLibs.140.00");

        Assert.True(framework.IsFramework);
        Assert.True(framework.HasFileUrl);
        Assert.Equal("22222222-2222-2222-2222-222222222222", framework.UpdateId);
        Assert.Equal(4, framework.RevisionNumber);
    }

    [Fact]
    public void SortsLargestPackagesFirst()
    {
        var packages = ParseFixture();

        Assert.Equal("Microsoft.VCLibs.140.00_14.0.33728.0_x64.appx", packages[0].ReadableFileName);
    }
}
