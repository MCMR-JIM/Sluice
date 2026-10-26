using System.Net;
using Sluice.Download;
using Sluice.Fe3;
using Sluice.Resolution;
using Xunit;

namespace Sluice.Tests;

public sealed class DownloadDeduplicationTests : IDisposable
{
    private static readonly byte[] Payload = System.Text.Encoding.UTF8.GetBytes(
        string.Concat(Enumerable.Repeat("sluice-dedupe-", 24)));

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Sluice-dedupe-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task FetchesOneFileOnceWhenTheSameNameAppearsTwice()
    {
        // Two entries for one destination used to start two writers on the same .part file; the second
        // one failed on a sharing violation that looked like a network error.
        Directory.CreateDirectory(_directory);
        var handler = new CountingHandler(Payload);
        using var downloader = new PackageDownloader(new HttpClient(handler));

        var results = await downloader.DownloadManyAsync(
            [CreatePackage("Contoso.Notes_1.0.0.0_x64.appx"), CreatePackage("Contoso.Notes_1.0.0.0_x64.appx")],
            _directory,
            new DownloadOptions(MaxParallelism: 4));

        Assert.Single(results);
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task KeepsFilesThatOnlyDifferInCaseTogether()
    {
        // Names that differ only in case are one file on Windows: the batch must not write twice.
        Directory.CreateDirectory(_directory);
        var handler = new CountingHandler(Payload);
        using var downloader = new PackageDownloader(new HttpClient(handler));

        var results = await downloader.DownloadManyAsync(
            [CreatePackage("Contoso.Notes_1.0.0.0_x64.appx"), CreatePackage("contoso.notes_1.0.0.0_x64.appx")],
            _directory,
            new DownloadOptions(MaxParallelism: 4));

        Assert.Single(results);
    }

    [Fact]
    public async Task StillDownloadsDifferentFilesInParallel()
    {
        Directory.CreateDirectory(_directory);
        var handler = new CountingHandler(Payload);
        using var downloader = new PackageDownloader(new HttpClient(handler));

        var results = await downloader.DownloadManyAsync(
            [CreatePackage("One_1.0.0.0_x64.appx"), CreatePackage("Two_1.0.0.0_x64.appx")],
            _directory,
            new DownloadOptions(MaxParallelism: 4));

        Assert.Equal(2, results.Count);
        Assert.Equal(2, handler.Requests);
    }

    private static ResolvedPackage CreatePackage(string fileName)
    {
        var package = new WuPackage(
            Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString(), 1, "Contoso.Notes", fileName, fileName,
            Payload.Length, null, null, $"{Path.GetFileNameWithoutExtension(fileName)}__abcdefghij",
            "x64", "1.0.0.0", false, false, true);

        return new ResolvedPackage(package, $"https://cdn.example.invalid/{fileName}");
    }

    private sealed class CountingHandler(byte[] payload) : HttpMessageHandler
    {
        private int _requests;

        public int Requests => Volatile.Read(ref _requests);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
            response.Content.Headers.ContentLength = payload.Length;
            return Task.FromResult(response);
        }
    }
}
