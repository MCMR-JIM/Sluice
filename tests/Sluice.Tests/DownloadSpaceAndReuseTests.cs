using System.Net;
using System.Security.Cryptography;
using Sluice.Download;
using Sluice.Fe3;
using Sluice.Resolution;
using Xunit;

namespace Sluice.Tests;

public sealed class DownloadSpaceAndReuseTests : IDisposable
{
    private static readonly byte[] Payload = System.Text.Encoding.UTF8.GetBytes(
        string.Concat(Enumerable.Repeat("sluice-space-marker-", 32)));

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Sluice-space-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void RefusesADownloadThatCannotFit()
    {
        var problem = FreeSpace.Check(5L * 1024 * 1024 * 1024, 2L * 1024 * 1024 * 1024, @"D:\packages");

        Assert.NotNull(problem);
        Assert.Contains("GiB", problem, StringComparison.Ordinal);
        Assert.Contains(@"D:\packages", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowsADownloadThatFitsWithHeadroom()
    {
        Assert.Null(FreeSpace.Check(1024, 10L * 1024 * 1024 * 1024, "cache"));

        // Exactly the required amount is not enough: the partial file and the manifest need room too.
        Assert.NotNull(FreeSpace.Check(FreeSpace.ReserveBytes, 10, "cache"));
    }

    [Fact]
    public void DoesNotBlockWhenTheDriveCannotBeInspected()
    {
        Assert.Null(FreeSpace.Check(long.MaxValue / 2, long.MaxValue, @"\\server\share"));
    }

    [Fact]
    public void ReadsTheSpaceOfThisMachinesTempFolder()
    {
        Assert.True(FreeSpace.AvailableBytes(Path.GetTempPath()) > 0);
    }

    [Fact]
    public async Task DoesNotReuseALeftoverOfTheWrongSize()
    {
        // An interrupted run leaves a truncated file behind. Reusing it (which is what happened when
        // verification was off) handed the install a package that could not be opened.
        Directory.CreateDirectory(_directory);
        var package = CreatePackage();
        var target = Path.Combine(_directory, package.Package.ReadableFileName);
        await File.WriteAllBytesAsync(target, Payload[..16]);

        using var downloader = new PackageDownloader(new HttpClient(new EchoHandler(Payload)));
        var result = await downloader.DownloadAsync(package, _directory, new DownloadOptions(VerifyDigest: false));

        Assert.False(result.Reused);
        Assert.Equal(Payload, await File.ReadAllBytesAsync(result.Path));
    }

    [Fact]
    public async Task ReusesACompleteFileWithoutFetchingItAgain()
    {
        Directory.CreateDirectory(_directory);
        var package = CreatePackage();
        var target = Path.Combine(_directory, package.Package.ReadableFileName);
        await File.WriteAllBytesAsync(target, Payload);

        var handler = new EchoHandler(Payload);
        using var downloader = new PackageDownloader(new HttpClient(handler));
        var result = await downloader.DownloadAsync(package, _directory, new DownloadOptions(VerifyDigest: false));

        Assert.True(result.Reused);
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task DownloadsSeveralFilesThroughTheSpaceCheck()
    {
        // The check runs before the first byte is fetched, so this proves it does not block a run that
        // fits and that the batch path still works.
        Directory.CreateDirectory(_directory);
        var package = CreatePackage();
        using var downloader = new PackageDownloader(new HttpClient(new EchoHandler(Payload)));

        var results = await downloader.DownloadManyAsync([package, package], _directory, new DownloadOptions());

        Assert.Equal(Payload.Length, results[0].SizeInBytes);
    }

    private static ResolvedPackage CreatePackage()
    {
#pragma warning disable CA5350
        var sha1 = Convert.ToBase64String(SHA1.HashData(Payload));
#pragma warning restore CA5350
        var package = new WuPackage(
            "1", Guid.NewGuid().ToString(), 1, "Contoso.Notes", "Contoso.Notes_1.0.0.0_x64.appx",
            "Contoso.Notes_1.0.0.0_x64.appx", Payload.Length, sha1, "sha256",
            "Contoso.Notes_1.0.0.0_x64__abcdefghij", "x64", "1.0.0.0", false, false, true);

        return new ResolvedPackage(package, "https://cdn.example.invalid/package.appx");
    }

    private sealed class EchoHandler(byte[] payload) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
            response.Content.Headers.ContentLength = payload.Length;
            return Task.FromResult(response);
        }
    }
}
