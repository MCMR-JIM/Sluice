using Sluice.Download;
using Sluice.Fe3;
using Sluice.Resolution;
using Sluice.Store;
using Xunit;

namespace Sluice.Tests;

public sealed class FolderSelectionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Sluice-folder-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task WithoutAManifestTheWholeFolderIsOffered()
    {
        Directory.CreateDirectory(_directory);
        var files = new[] { Path.Combine(_directory, "a.appx"), Path.Combine(_directory, "b.appx") };

        var selection = await DownloadManifest.SelectAsync(_directory, files);

        Assert.False(selection.FromManifest);
        Assert.Equal(2, selection.Files.Count);
        Assert.Empty(selection.Missing);
    }

    [Fact]
    public async Task AManifestNarrowsAFolderSharedByTwoProducts()
    {
        // A folder reused for several products offered the planner every package in it, and the main
        // package was chosen by architecture and size - so the wrong app could be installed.
        Directory.CreateDirectory(_directory);
        var wanted = Path.Combine(_directory, "Contoso.Notes_1.0.0.0_x64.appx");
        var other = Path.Combine(_directory, "Contoso.Other_1.0.0.0_x64.appx");
        await File.WriteAllBytesAsync(wanted, [1, 2, 3]);
        await File.WriteAllBytesAsync(other, [4, 5, 6]);
        await SaveManifestAsync("Contoso.Notes_1.0.0.0_x64.appx");

        var selection = await DownloadManifest.SelectAsync(_directory, [wanted, other]);

        Assert.True(selection.FromManifest);
        Assert.Single(selection.Files);
        Assert.Equal(wanted, selection.Files[0]);
    }

    [Fact]
    public async Task AManifestWhoseFilesAreGoneFallsBackToTheFolder()
    {
        Directory.CreateDirectory(_directory);
        var present = Path.Combine(_directory, "Contoso.Notes_1.0.0.0_x64.appx");
        await File.WriteAllBytesAsync(present, [1, 2, 3]);
        await SaveManifestAsync("Contoso.Deleted_9.9.9.9_x64.appx");

        var selection = await DownloadManifest.SelectAsync(_directory, [present]);

        Assert.False(selection.FromManifest);
        Assert.Single(selection.Files);

        // The stale entry is reported rather than silently dropped when the folder does answer.
        await SaveManifestAsync("Contoso.Notes_1.0.0.0_x64.appx", "Contoso.Deleted_9.9.9.9_x64.appx");
        var partial = await DownloadManifest.SelectAsync(_directory, [present]);

        Assert.True(partial.FromManifest);
        Assert.Single(partial.Files);
        Assert.Equal(["Contoso.Deleted_9.9.9.9_x64.appx"], partial.Missing);
    }

    private async Task SaveManifestAsync(params string[] fileNames)
    {
        var resolution = new PackageResolution(
            new StoreProduct("9TESTTEST01", "Contoso Notes", "Contoso", "Contoso.Notes_abcdefghij",
                [new StorePackageGroup("11111111-1111-1111-1111-111111111111", "MsixBundle", "Contoso.Notes_abcdefghij", ["x64"])],
                "US", "en-US", "displaycatalog"),
            []);

        var results = fileNames.Select((fileName, index) =>
        {
            var package = new WuPackage($"{index}", Guid.NewGuid().ToString(), 1, "Contoso.Notes", fileName, fileName,
                3, null, null, "Contoso.Notes_1.0.0.0_x64__abcdefghij", "x64", "1.0.0.0", false, false, true);
            return new DownloadResult(package, Path.Combine(_directory, fileName), 3, DownloadVerification.Skipped, false);
        }).ToList();

        await DownloadManifest.SaveAsync(_directory, DownloadManifest.FromResults(resolution, results, "x64", "en-US"));
    }
}
