using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Sluice.Download;
using Sluice.Fe3;
using Sluice.Resolution;
using Xunit;

namespace Sluice.Tests;

public sealed class PackageDownloaderTests : IDisposable
{
    private static readonly byte[] Payload = System.Text.Encoding.UTF8.GetBytes(
        string.Concat(Enumerable.Repeat("sluice-payload-marker-", 64)));

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Sluice-pkgdl-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task RecoversFromACorruptPartialFileInsteadOfResumingIt()
    {
        Directory.CreateDirectory(_directory);
        var package = CreatePackage();
        var partial = Path.Combine(_directory, package.Package.ReadableFileName) + ".part";

        // What a cancelled or crashed run leaves behind. The bytes are wrong, so resuming from them
        // fails verification; the download has to start over instead of retrying the same prefix.
        await File.WriteAllBytesAsync(partial, "corrupt-prefix"u8.ToArray());

        using var downloader = new PackageDownloader(new HttpClient(new RangeHandler(Payload)));
        var result = await downloader.DownloadAsync(package, _directory, new DownloadOptions(VerifyDigest: true));

        Assert.Equal(DownloadVerification.DigestMatch, result.Verification);
        Assert.Equal(Payload, await File.ReadAllBytesAsync(result.Path));
        Assert.False(File.Exists(partial));
    }

    [Fact]
    public async Task FailsAndLeavesNoPartialFileWhenTheContentNeverMatches()
    {
        Directory.CreateDirectory(_directory);
        var package = CreatePackage();
        var partial = Path.Combine(_directory, package.Package.ReadableFileName) + ".part";

        // The server keeps serving bytes that are not what the delivery service published, so no
        // amount of retrying can help; the attempt has to end with a clear failure and a clean slate.
        using var downloader = new PackageDownloader(new HttpClient(new RangeHandler(Payload[..32])));
        await Assert.ThrowsAsync<DownloadException>(
            () => downloader.DownloadAsync(package, _directory, new DownloadOptions(VerifyDigest: true)));

        Assert.False(File.Exists(partial), "a file that cannot verify must not be left for the next run");
    }

    private static ResolvedPackage CreatePackage()
    {
        // SHA-1 is what the delivery service publishes, so the test has to speak the same language.
#pragma warning disable CA5350
        var sha1 = Convert.ToBase64String(SHA1.HashData(Payload));
#pragma warning restore CA5350
        var package = new WuPackage(
            "1",
            Guid.NewGuid().ToString(),
            1,
            "Contoso.Notes",
            "Contoso.Notes_1.0.0.0_x64.appx",
            "Contoso.Notes_1.0.0.0_x64.appx",
            Payload.Length,
            sha1,
            "sha256",
            "Contoso.Notes_1.0.0.0_x64__abcdefghij",
            "x64",
            "1.0.0.0",
            false,
            false,
            true);

        return new ResolvedPackage(package, "https://cdn.example.invalid/package.appx");
    }

    /// <summary>Serves a payload and honours Range requests the way the delivery CDN does.</summary>
    private sealed class RangeHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var from = request.Headers.Range?.Ranges.FirstOrDefault()?.From ?? 0;
            var offset = (int)Math.Clamp(from, 0, payload.Length);
            var body = payload[offset..];

            var response = new HttpResponseMessage(offset > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(body),
            };

            response.Content.Headers.ContentLength = body.Length;
            if (offset > 0)
            {
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, payload.Length - 1, payload.Length);
            }

            return Task.FromResult(response);
        }
    }
}
