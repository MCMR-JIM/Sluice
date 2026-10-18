using StoreDownloader.Download;
using StoreDownloader.Fe3;
using StoreDownloader.Resolution;
using StoreDownloader.Store;
using Xunit;

namespace StoreDownloader.Tests;

public sealed class DownloadTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "storedl-dl-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("Contoso.App_1.0.0.0_x64.msixbundle", "Contoso.App_1.0.0.0_x64.msixbundle")]
    [InlineData("bad:name?.appx", "bad_name_.appx")]
    [InlineData("Microsoft.VCLibs.140.00_14.0_x64.appx", "Microsoft.VCLibs.140.00_14.0_x64.appx")]
    public void SanitisesFileNames(string input, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.Sanitize(input));
    }

    [Fact]
    public async Task SavesAndLoadsAManifest()
    {
        Directory.CreateDirectory(_directory);
        var resolution = new PackageResolution(
            new StoreProduct("9TESTTEST01", "Contoso Notes", "Contoso", "Contoso.Notes_abcdefghij",
                [new StorePackageGroup("11111111-1111-1111-1111-111111111111", "MsixBundle", "Contoso.Notes_abcdefghij", ["x64"])],
                "US", "en-US", "displaycatalog"),
            []);

        var package = new WuPackage("1", Guid.NewGuid().ToString(), 1, "Contoso.Notes", "a.msixbundle",
            "Contoso.Notes_2.0.0.0_neutral.msixbundle", 1234, "sha1", "sha256",
            "Contoso.Notes_2.0.0.0_neutral__abcdefghij", "neutral", "2.0.0.0", true, false, true);
        var result = new DownloadResult(package, Path.Combine(_directory, "Contoso.Notes_2.0.0.0_neutral.msixbundle"), 1234, DownloadVerification.DigestMatch, false);

        var manifest = DownloadManifest.FromResults(resolution, [result], "x64", "en-US");
        await DownloadManifest.SaveAsync(_directory, manifest);

        var loaded = await DownloadManifest.LoadAsync(_directory);

        Assert.NotNull(loaded);
        Assert.Equal("9TESTTEST01", loaded!.ProductId);
        Assert.Equal("x64", loaded.Architecture);
        var entry = Assert.Single(loaded.Packages);
        Assert.Equal("Contoso.Notes_2.0.0.0_neutral.msixbundle", entry.FileName);
        Assert.Equal("sha256", entry.Sha256);
    }

    [Fact]
    public async Task ReturnsNullWhenNoManifestExists()
    {
        Directory.CreateDirectory(_directory);

        Assert.Null(await DownloadManifest.LoadAsync(_directory));
    }

    [Fact]
    public async Task RefusesPackagesWithoutAUrl()
    {
        using var downloader = new PackageDownloader();
        var package = new WuPackage("1", Guid.NewGuid().ToString(), 1, "Contoso.Notes", "a.appx", "Contoso.Notes_1.0.0.0_x64.appx",
            10, "sha1", "sha256", "Contoso.Notes_1.0.0.0_x64__abcdefghij", "x64", "1.0.0.0", false, false, false);

        await Assert.ThrowsAsync<DownloadException>(
            () => downloader.DownloadAsync(new ResolvedPackage(package, null), _directory));
    }
}
