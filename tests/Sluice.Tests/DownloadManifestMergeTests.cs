using Sluice.Download;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// A partial download (<c>--deps-only</c>, <c>--only</c>) used to replace the manifest with the files it
/// fetched. The folder still held the application, but the manifest no longer named it, so the next
/// <c>install --dir</c> planned against a folder that "has no application package" and refused to run.
/// </summary>
public sealed class DownloadManifestMergeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Sluice-merge-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task APartialRunKeepsTheFilesItDidNotDownload()
    {
        Directory.CreateDirectory(_directory);
        var app = Write("Contoso.Notes_1.0.0.0_x64.appx");
        var framework = Write("Microsoft.VCLibs.140.00_14.0.30704.0_x64.appx");

        await DownloadManifest.SaveMergingAsync(_directory, ManifestOf("9TESTTEST01", app, framework));
        await DownloadManifest.SaveMergingAsync(_directory, ManifestOf("9TESTTEST01", framework));

        var selection = await DownloadManifest.SelectAsync(_directory, [app, framework]);

        Assert.Equal(2, selection.Files.Count);
        Assert.Contains(app, selection.Files);
    }

    [Fact]
    public async Task EntriesOfAFileThatIsGoneAreDropped()
    {
        Directory.CreateDirectory(_directory);
        var app = Write("Contoso.Notes_1.0.0.0_x64.appx");
        var framework = Write("Microsoft.VCLibs.140.00_14.0.30704.0_x64.appx");

        await DownloadManifest.SaveMergingAsync(_directory, ManifestOf("9TESTTEST01", app, framework));
        File.Delete(framework);
        await DownloadManifest.SaveMergingAsync(_directory, ManifestOf("9TESTTEST01", app));

        var manifest = await DownloadManifest.LoadAsync(_directory);

        Assert.NotNull(manifest);
        Assert.Equal(["Contoso.Notes_1.0.0.0_x64.appx"], manifest!.Packages.Select(entry => entry.FileName));
    }

    [Fact]
    public async Task TheManifestOfAnotherProductIsReplaced()
    {
        Directory.CreateDirectory(_directory);
        var first = Write("Contoso.Notes_1.0.0.0_x64.appx");
        var second = Write("Contoso.Other_1.0.0.0_x64.appx");

        await DownloadManifest.SaveMergingAsync(_directory, ManifestOf("9TESTTEST01", first));
        await DownloadManifest.SaveMergingAsync(_directory, ManifestOf("9OTHERPROD1", second));

        var manifest = await DownloadManifest.LoadAsync(_directory);

        Assert.NotNull(manifest);
        Assert.Equal("9OTHERPROD1", manifest!.ProductId);
        Assert.Equal(["Contoso.Other_1.0.0.0_x64.appx"], manifest.Packages.Select(entry => entry.FileName));
    }

    private string Write(string fileName)
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    private static DownloadManifest ManifestOf(string productId, params string[] paths) =>
        new(
            productId,
            "Contoso Notes",
            "Contoso.Notes_abcdefghij",
            "x64",
            "en-US",
            DateTimeOffset.UnixEpoch,
            paths
                .Select(path => new ManifestEntry(
                    Path.GetFileName(path),
                    Path.GetFileNameWithoutExtension(path) + "__abcdefghij",
                    "Contoso.Notes",
                    "1.0.0.0",
                    "x64",
                    3,
                    null))
                .ToList());
}
