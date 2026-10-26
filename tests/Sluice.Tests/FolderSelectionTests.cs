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

    [Fact]
    public async Task AManifestEntryThatNamesAPathOutsideTheFolderIsIgnored()
    {
        // A manifest is a text file that sits next to the packages, and it used to be trusted as written:
        // Path.Combine returns its second argument unchanged when that one is rooted, so an entry naming
        // an absolute path - or one that walks back out with .. - made the plan read a package from
        // anywhere on the machine and install it instead of the one in the folder.
        Directory.CreateDirectory(_directory);
        var inside = Path.Combine(_directory, "Contoso.Notes_1.0.0.0_x64.appx");
        await File.WriteAllBytesAsync(inside, [1, 2, 3]);

        var outsideDirectory = Path.Combine(Path.GetTempPath(), "Sluice-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDirectory);
        var outside = Path.Combine(outsideDirectory, "Contoso.Other_1.0.0.0_x64.appx");
        await File.WriteAllBytesAsync(outside, [4, 5, 6]);

        try
        {
            await DownloadManifest.SaveAsync(_directory, ManifestOf(
                "Contoso.Notes_1.0.0.0_x64.appx",
                outside,
                ".." + Path.DirectorySeparatorChar + "Contoso.Other_1.0.0.0_x64.appx"));

            var selection = await DownloadManifest.SelectAsync(_directory, [inside]);

            Assert.Equal([inside], selection.Files);
            Assert.Contains(outside, selection.Ignored);
            Assert.Contains(".." + Path.DirectorySeparatorChar + "Contoso.Other_1.0.0.0_x64.appx", selection.Ignored);
            Assert.Empty(selection.Missing);
        }
        finally
        {
            Directory.Delete(outsideDirectory, recursive: true);
        }
    }

    private static DownloadManifest ManifestOf(params string[] fileNames) =>
        new(
            "9TESTTEST01",
            "Contoso Notes",
            "Contoso.Notes_abcdefghij",
            "x64",
            "en-US",
            DateTimeOffset.UnixEpoch,
            fileNames
                .Select(fileName => new ManifestEntry(fileName, "Contoso.Notes_1.0.0.0_x64__abcdefghij", "Contoso.Notes", "1.0.0.0", "x64", 3, null))
                .ToList());

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
