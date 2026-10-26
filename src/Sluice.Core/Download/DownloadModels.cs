using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Sluice.Fe3;
using Sluice.Http;
using Sluice.Resolution;

namespace Sluice.Download;

/// <summary>Options that control a download run.</summary>
/// <param name="MaxParallelism">How many files may be downloaded at once.</param>
/// <param name="Overwrite">Re-download files that already exist and verify, instead of skipping them.</param>
/// <param name="VerifyDigest">Verify the SHA-1 digest the delivery service published.</param>
/// <param name="Progress">Callback invoked with throttled progress updates.</param>
/// <param name="ProgressIntervalBytes">
/// How much data may pass between two progress callbacks. Reporting every buffer made a large download
/// push thousands of updates per file at the window, each one a dispatcher hop.
/// </param>
public sealed record DownloadOptions(
    int MaxParallelism = 3,
    bool Overwrite = false,
    bool VerifyDigest = true,
    Action<DownloadProgress>? Progress = null,
    long ProgressIntervalBytes = DownloadOptions.DefaultProgressIntervalBytes)
{
    /// <summary>Default span between progress callbacks: one mebibyte.</summary>
    public const long DefaultProgressIntervalBytes = 1024 * 1024;

    /// <summary>Options used when the caller supplies none.</summary>
    public static DownloadOptions Default { get; } = new();
}

/// <summary>Progress of a single file.</summary>
/// <param name="FileName">File being downloaded.</param>
/// <param name="BytesReceived">Bytes written so far.</param>
/// <param name="TotalBytes">Expected size, or 0 when the server did not say.</param>
public sealed record DownloadProgress(string FileName, long BytesReceived, long TotalBytes)
{
    /// <summary>Completion ratio between 0 and 1, or 0 when the size is unknown.</summary>
    public double Fraction => TotalBytes > 0 ? Math.Clamp((double)BytesReceived / TotalBytes, 0, 1) : 0;
}

/// <summary>How a downloaded file was validated.</summary>
public enum DownloadVerification
{
    /// <summary>No digest was published, so nothing was checked.</summary>
    NotPublished,

    /// <summary>The published digest matched.</summary>
    DigestMatch,

    /// <summary>Verification was disabled by the caller.</summary>
    Skipped,

    /// <summary>The published digest did not match the bytes on disk.</summary>
    Mismatch,
}

/// <summary>Outcome of downloading one package.</summary>
/// <param name="Package">Package that was downloaded.</param>
/// <param name="Path">Where the file lives on disk.</param>
/// <param name="SizeInBytes">Size on disk.</param>
/// <param name="Verification">How the file was validated.</param>
/// <param name="Reused">True when an existing, already valid file was reused.</param>
public sealed record DownloadResult(
    WuPackage Package,
    string Path,
    long SizeInBytes,
    DownloadVerification Verification,
    bool Reused);

/// <summary>Raised when a package cannot be downloaded or fails verification.</summary>
public sealed class DownloadException : SluiceException
{
    /// <summary>Creates an exception with a message.</summary>
    /// <param name="message">Human readable description.</param>
    /// <param name="innerException">Underlying failure.</param>
    public DownloadException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}

/// <summary>Makes package file names safe for the file system.</summary>
/// <remarks>
/// The Windows character set is applied on every platform on purpose: these are appx packages
/// that end up in a Windows deployment, and a name that is legal on Linux but illegal on Windows
/// produces a confusing failure much later instead of here.
/// </remarks>
public static class FileNameSanitizer
{
    private const string InvalidCharacters = "<>:\"|?*/\\";

    /// <summary>Replaces characters that are invalid in Windows file names.</summary>
    /// <param name="fileName">Candidate file name.</param>
    public static string Sanitize(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var builder = new System.Text.StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            var invalid = InvalidCharacters.Contains(character, StringComparison.Ordinal) || char.IsControl(character);
            builder.Append(invalid ? '_' : character);
        }

        // Windows silently drops trailing dots and spaces, which would desynchronise the file name
        // from the digest the manifest recorded for it.
        var sanitized = builder.ToString().TrimEnd('.', ' ');
        return sanitized.Length == 0 ? "_" : sanitized;
    }
}
