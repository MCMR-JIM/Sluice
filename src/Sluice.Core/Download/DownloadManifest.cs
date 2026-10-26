using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sluice.Download;

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
    public const string FileName = "Sluice-manifest.json";

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

/// <summary>
/// Narrows a folder of packages down to the ones a manifest names. A folder reused for several products
/// otherwise offers the planner every file it holds, and picking the wrong one installs the wrong app.
/// </summary>
/// <param name="directory">Folder holding the packages and, optionally, a manifest.</param>
/// <param name="allPackageFiles">Every package file the caller found in the folder.</param>
/// <param name="cancellationToken">Cancellation token.</param>
public static async Task<FolderSelection> SelectAsync(
    string directory,
    IReadOnlyList<string> allPackageFiles,
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(directory);
    ArgumentNullException.ThrowIfNull(allPackageFiles);

    var manifest = await LoadAsync(directory, cancellationToken).ConfigureAwait(false);
    if (manifest is null || manifest.Packages.Count == 0)
    {
        return FolderSelection.Everything(allPackageFiles);
    }

    var files = new List<string>();
    var missing = new List<string>();
    var ignored = new List<string>();
    foreach (var entry in manifest.Packages)
    {
        if (!IsFileNameInFolder(entry.FileName))
        {
            // The manifest is a text file that sits next to the packages, and a hand written one used to
            // name anything on the machine: Path.Combine returns its second argument unchanged when that
            // one is rooted, so the plan happily installed a package from outside the folder.
            ignored.Add(entry.FileName);
            continue;
        }

        var path = Path.Combine(directory, entry.FileName);
        if (File.Exists(path))
        {
            files.Add(path);
        }
        else
        {
            missing.Add(entry.FileName);
        }
    }

    // A manifest that names nothing found on disk is worse than no manifest at all: fall back to the
    // folder contents so a hand-replaced download still installs.
    return files.Count == 0
        ? FolderSelection.Everything(allPackageFiles) with { Ignored = ignored }
        : new FolderSelection(files, missing, true, ignored);
}

/// <summary>
/// True when a manifest entry names a file inside the folder: a bare name, with no directory part and
/// no way to walk back out of it.
/// </summary>
/// <param name="fileName">Entry from the manifest.</param>
private static bool IsFileNameInFolder(string fileName)
{
    if (string.IsNullOrWhiteSpace(fileName))
    {
        return false;
    }

    if (Path.IsPathRooted(fileName) || fileName.Contains(':', StringComparison.Ordinal))
    {
        return false;
    }

    if (fileName.Contains('/', StringComparison.Ordinal) || fileName.Contains('\\', StringComparison.Ordinal))
    {
        return false;
    }

    return string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal)
        && fileName is not "." and not "..";
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
