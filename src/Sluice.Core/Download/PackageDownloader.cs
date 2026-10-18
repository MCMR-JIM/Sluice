using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Sluice.Fe3;
using Sluice.Http;
using Sluice.Resolution;

namespace Sluice.Download;

/// <summary>
/// Downloads packages from the delivery CDN: resumable, parallel, and verified against the
/// digest the delivery service published for the file.
/// </summary>
public sealed class PackageDownloader : IDisposable
{
    private const int CopyBufferSize = 128 * 1024;

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    /// <summary>Creates a downloader, optionally over a caller owned <see cref="HttpClient"/>.</summary>
    /// <param name="httpClient">Client to use; a download tuned client is created when omitted.</param>
    public PackageDownloader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? StoreHttpClient.CreateDownloadClient();
        _ownsClient = httpClient is null;
    }

    /// <summary>Downloads every package, honouring the parallelism limit.</summary>
    /// <param name="packages">Packages to download.</param>
    /// <param name="directory">Destination directory.</param>
    /// <param name="options">Download options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<DownloadResult>> DownloadManyAsync(
        IEnumerable<ResolvedPackage> packages,
        string directory,
        DownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);

        options ??= DownloadOptions.Default;
        Directory.CreateDirectory(directory);

        var results = new System.Collections.Concurrent.ConcurrentBag<DownloadResult>();
        await Parallel.ForEachAsync(
            packages,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, options.MaxParallelism),
                CancellationToken = cancellationToken,
            },
            async (package, token) =>
            {
                var result = await DownloadAsync(package, directory, options, token).ConfigureAwait(false);
                results.Add(result);
            }).ConfigureAwait(false);

        return results.OrderBy(result => result.Package.ReadableFileName, StringComparer.Ordinal).ToList();
    }

    /// <summary>Downloads one package.</summary>
    /// <param name="package">Package to download, including its URL.</param>
    /// <param name="directory">Destination directory.</param>
    /// <param name="options">Download options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DownloadResult> DownloadAsync(
        ResolvedPackage package,
        string directory,
        DownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        options ??= DownloadOptions.Default;
        if (!package.IsDownloadable)
        {
            throw new DownloadException($"'{package.Package.ReadableFileName}' has no download url; resolve the product again.");
        }

        Directory.CreateDirectory(directory);
        var targetPath = Path.Combine(directory, FileNameSanitizer.Sanitize(package.Package.ReadableFileName));
        var partialPath = targetPath + ".part";

        if (!options.Overwrite && File.Exists(targetPath))
        {
            var existingVerification = options.VerifyDigest
                ? await VerifyAsync(targetPath, package.Package, cancellationToken).ConfigureAwait(false)
                : DownloadVerification.Skipped;

            if (existingVerification is DownloadVerification.DigestMatch or DownloadVerification.Skipped or DownloadVerification.NotPublished)
            {
                var existingSize = new FileInfo(targetPath).Length;
                return new DownloadResult(package.Package, targetPath, existingSize, existingVerification, Reused: true);
            }

            // A mismatching leftover file is worthless; start over instead of trusting it.
            File.Delete(targetPath);
        }

        Exception? lastError = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await DownloadOnceAsync(package, partialPath, options, cancellationToken).ConfigureAwait(false);

                var verification = options.VerifyDigest
                    ? await VerifyAsync(partialPath, package.Package, cancellationToken).ConfigureAwait(false)
                    : DownloadVerification.Skipped;

                if (verification == DownloadVerification.Mismatch)
                {
                    throw new DownloadException($"'{package.Package.ReadableFileName}' failed digest verification.");
                }

                File.Move(partialPath, targetPath, overwrite: true);
                var size = new FileInfo(targetPath).Length;
                return new DownloadResult(package.Package, targetPath, size, verification, Reused: false);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException && !cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new DownloadException(
            $"Failed to download '{package.Package.ReadableFileName}' after 3 attempts.",
            lastError);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task DownloadOnceAsync(
        ResolvedPackage package,
        string partialPath,
        DownloadOptions options,
        CancellationToken cancellationToken)
    {
        var resumeFrom = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, package.DownloadUrl);
        if (resumeFrom > 0)
        {
            request.Headers.Range = new RangeHeaderValue(resumeFrom, null);
        }

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && resumeFrom > 0)
        {
            // The partial file is not what the server expects; start clean.
            File.Delete(partialPath);
            await DownloadOnceAsync(package, partialPath, options, cancellationToken).ConfigureAwait(false);
            return;
        }

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength is { } length
            ? length + (response.StatusCode == HttpStatusCode.PartialContent ? resumeFrom : 0)
            : package.Package.SizeInBytes;

        var append = response.StatusCode == HttpStatusCode.PartialContent && resumeFrom > 0;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = new FileStream(
            partialPath,
            append ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            useAsync: true);

        var received = append ? resumeFrom : 0;
        var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, CopyBufferSize), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                received += read;
                options.Progress?.Invoke(new DownloadProgress(package.Package.ReadableFileName, received, total));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (total > 0 && received < total)
        {
            throw new IOException($"'{package.Package.ReadableFileName}' ended after {received} of {total} bytes.");
        }
    }

    // SHA-1 is what the delivery service publishes for each file, so it is the only digest that
    // can be checked here; it is an integrity check against CDN corruption, not a security boundary.
#pragma warning disable CA5350
    private static async Task<DownloadVerification> VerifyAsync(string path, WuPackage package, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(package.Sha1Digest))
        {
            return DownloadVerification.NotPublished;
        }

        await using var stream = File.OpenRead(path);
        var hash = await SHA1.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actual = Convert.ToBase64String(hash);
        return string.Equals(actual, package.Sha1Digest, StringComparison.Ordinal)
            ? DownloadVerification.DigestMatch
            : DownloadVerification.Mismatch;
    }
#pragma warning restore CA5350
}
