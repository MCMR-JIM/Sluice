namespace StoreDownloader.Fe3;

/// <summary>
/// One downloadable file behind a Windows Update category, as described by the delivery
/// service (FE3) in its <c>SyncUpdates</c> answer.
/// </summary>
/// <param name="NumericId">Update id used inside the response document.</param>
/// <param name="UpdateId">Update GUID used by <c>GetExtendedUpdateInfo2</c>.</param>
/// <param name="RevisionNumber">Revision paired with <paramref name="UpdateId"/>.</param>
/// <param name="PackageIdentityName">Package identity name, for example <c>Microsoft.VCLibs.140.00</c>.</param>
/// <param name="DeliveryFileName">Opaque name the CDN serves the file under.</param>
/// <param name="ReadableFileName">Name rebuilt from the installer specific identifier.</param>
/// <param name="SizeInBytes">Size reported by the delivery service.</param>
/// <param name="Sha1Digest">Base64 SHA-1 digest; the key that maps to a download URL.</param>
/// <param name="Sha256Digest">Base64 SHA-256 digest of the package payload, when published.</param>
/// <param name="InstallerSpecificIdentifier">Identifier of the form <c>Name_Version_Arch__PublisherId</c>.</param>
/// <param name="Architecture">Architecture parsed out of <paramref name="InstallerSpecificIdentifier"/>.</param>
/// <param name="Version">Version parsed out of <paramref name="InstallerSpecificIdentifier"/>.</param>
/// <param name="IsMainPackage">True when the delivery service marks this as the main package.</param>
/// <param name="IsFramework">True when the delivery service marks this as an appx framework.</param>
/// <param name="HasFileUrl">True when the update carried a secured fragment, i.e. has a resolvable URL.</param>
public sealed record WuPackage(
    string NumericId,
    string UpdateId,
    int RevisionNumber,
    string? PackageIdentityName,
    string DeliveryFileName,
    string ReadableFileName,
    long SizeInBytes,
    string? Sha1Digest,
    string? Sha256Digest,
    string InstallerSpecificIdentifier,
    string? Architecture,
    string? Version,
    bool IsMainPackage,
    bool IsFramework,
    bool HasFileUrl)
{
    /// <summary>File extension such as <c>.msixbundle</c>.</summary>
    public string Extension => Path.GetExtension(ReadableFileName);

    /// <summary>True when the payload is a bundle rather than a single package.</summary>
    public bool IsBundle => Extension is ".msixbundle" or ".appxbundle";

    /// <summary>True when the payload is a single package.</summary>
    public bool IsSinglePackage => Extension is ".msix" or ".appx";

    /// <inheritdoc />
    public override string ToString() => $"{ReadableFileName} ({SizeInBytes / 1024d / 1024d:F1} MiB)";
}
