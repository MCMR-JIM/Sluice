using System.Net;
using Sluice.Download;
using Sluice.Fe3;
using Sluice.Resolution;
using Xunit;

namespace Sluice.Tests;

public sealed class DownloadProgressTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Sluice-progress-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task ReportsProgressOncePerIntervalAndAlwaysAtTheEnd()
    {
        // Reporting every 128 KiB buffer meant thousands of dispatcher hops for a large package.
        Directory.CreateDirectory(_directory);
        var payload = new byte[5 * 1024 * 1024];
        var package = CreatePackage(payload.Length);
        var updates = new List<DownloadProgress>();
        var options = new DownloadOptions(
            Progress: updates.Add,
            ProgressIntervalBytes: 2 * 1024 * 1024,
            VerifyDigest: false);

        using var downloader = new PackageDownloader(new HttpClient(new PayloadHandler(payload)));
        await downloader.DownloadAsync(package, _directory, options);

        Assert.InRange(updates.Count, 2, 4);
        Assert.Equal(payload.Length, updates[^1].BytesReceived);
        Assert.Equal(1d, updates[^1].Fraction, 3);
    }

    [Fact]
    public async Task ReportsNothingForAnEmptyFile()
    {
        Directory.CreateDirectory(_directory);
        var package = CreatePackage(0);
        var updates = new List<DownloadProgress>();

        using var downloader = new PackageDownloader(new HttpClient(new PayloadHandler([])));
        await downloader.DownloadAsync(package, _directory, new DownloadOptions(Progress: updates.Add, VerifyDigest: false));

        Assert.Empty(updates);
    }

    private static ResolvedPackage CreatePackage(int size)
    {
        var name = "Contoso.Notes_1.0.0.0_x64.appx";
        var package = new WuPackage(
            "1", Guid.NewGuid().ToString(), 1, "Contoso.Notes", name, name, size, null, null,
            "Contoso.Notes_1.0.0.0_x64__abcdefghij", "x64", "1.0.0.0", false, false, true);

        return new ResolvedPackage(package, "https://cdn.example.invalid/package.appx");
    }

    private sealed class PayloadHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
            response.Content.Headers.ContentLength = payload.Length;
            return Task.FromResult(response);
        }
    }
}
