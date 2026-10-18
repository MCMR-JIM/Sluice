using System.Text.Json;
using System.Text.Json.Serialization;

namespace StoreDownloader.Download;

/// <summary>One package recorded in a download manifest.</summary>
/// <param name="FileName">File name inside the download directory.</param>
/// <param name="Identity">Installer specific identifier from the delivery service.</param>
/// <param name="PackageName">Package identity name.</param>
/// <param name="Version">Package version.</param>
/// <param name="Architecture">Package architecture.</param>
/// <param name="SizeInBytes">File size.</param>
/// <param name="Sha256">Base64 SHA-256 published by the delivery service, when available.</param>
public sealed record ManifestEntry(
    string FileName,
    string Identity,
    string? PackageName,
    string? Version,
    string? Architecture,
    long SizeInBytes,
    string? Sha256);

/// <summary>
/// Written next to downloaded packages so a later install run (or another machine) does not
/// have to resolve the product again.
/// </summary>
/// <param name="ProductId">Store product id.</param>
/// <param name="Title">Product title.</param>
/// <param name="PackageFamilyName">Package family name, when known.</param>
/// <param name="Architecture">Architecture the selection targeted.</param>
/// <param name="Locale">Locale the selection targeted.</param>
/// <param name="ResolvedAtUtc">When the packages were resolved.</param>
/// <param name="Packages">Downloaded packages.</param>
public sealed record DownloadManifest(
    string ProductId,
    string Title,
    string? PackageFamilyName,
    string Architecture,
    string Locale,
    DateTimeOffset ResolvedAtUtc,
    IReadOnlyList<ManifestEntry> Packages)
{
    /// <summary>File name of the manifest inside a download directory.</summary>
    public const string FileName = "StoreDownloader-manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Writes the manifest into <paramref name="directory"/>.</summary>
    /// <param name="directory">Download directory.</param>
    /// <param name="manifest">Manifest to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SaveAsync(string directory, DownloadManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var path = Path.Combine(directory, FileName);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, manifest, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a manifest from a directory, returning <c>null</c> when there is none.</summary>
    /// <param name="directory">Download directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<DownloadManifest?> LoadAsync(string directory, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<DownloadManifest>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Creates a manifest from downloaded packages.</summary>
    /// <param name="resolution">Resolution the packages came from.</param>
    /// <param name="results">Download results.</param>
    /// <param name="architecture">Architecture the selection targeted.</param>
    /// <param name="locale">Locale the selection targeted.</param>
    public static DownloadManifest FromResults(
        Resolution.PackageResolution resolution,
        IEnumerable<DownloadResult> results,
        string architecture,
        string locale)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(results);

        var entries = results
            .Select(result => new ManifestEntry(
                Path.GetFileName(result.Path),
                result.Package.InstallerSpecificIdentifier,
                result.Package.PackageIdentityName,
                result.Package.Version,
                result.Package.Architecture,
                result.SizeInBytes,
                result.Package.Sha256Digest))
            .ToList();

        return new DownloadManifest(
            resolution.Product.ProductId,
            resolution.Product.Title,
            resolution.Product.PackageFamilyName,
            architecture,
            locale,
            DateTimeOffset.UtcNow,
            entries);
    }
}
