using System.Globalization;
using System.Xml.Linq;

namespace StoreDownloader.Fe3;

/// <summary>
/// Parses a <c>SyncUpdates</c> answer into <see cref="WuPackage"/> instances.
/// </summary>
/// <remarks>
/// The answer is split in two halves that have to be joined on the update's numeric id:
/// <list type="bullet">
///   <item>
///     <c>NewUpdates/UpdateInfo</c> carries the update identity (GUID + revision) and a
///     <c>SecuredFragment</c> marker telling which updates have a resolvable file URL,
///   </item>
///   <item>
///     <c>ExtendedUpdateInfo/Updates/Update</c> carries the payload description: package
///     identity, delivery file name, hashes and the installer specific identifier.
///   </item>
/// </list>
/// </remarks>
public static class SyncUpdatesParser
{
    /// <summary>Parses a raw response body.</summary>
    /// <param name="responseXml">SOAP response body.</param>
    /// <returns>Packages described by the response, excluding block map side files.</returns>
    public static IReadOnlyList<WuPackage> Parse(string responseXml) => Parse(WuSoap.ParseDocument(responseXml));

    /// <summary>Parses a loaded response document.</summary>
    /// <param name="document">SOAP response document.</param>
    /// <returns>Packages described by the response, excluding block map side files.</returns>
    public static IReadOnlyList<WuPackage> Parse(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var identities = CollectIdentities(document);
        var extendedRoot = FindFirst(document, "ExtendedUpdateInfo");
        if (extendedRoot is null)
        {
            return [];
        }

        var packages = new List<WuPackage>();
        foreach (var update in extendedRoot.Descendants().Where(element => element.Name.LocalName == "Update"))
        {
            var numericId = ChildValue(update, "ID");
            var xmlFragment = Child(update, "Xml");
            if (numericId is null || xmlFragment is null)
            {
                continue;
            }

            var fragment = ParseFragment(xmlFragment.Value);
            var extendedProperties = fragment.Descendants().FirstOrDefault(element => element.Name.LocalName == "ExtendedProperties");
            var packageIdentityName = extendedProperties?.Attribute("PackageIdentityName")?.Value;
            var isFramework = string.Equals(extendedProperties?.Attribute("IsAppxFramework")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            var installData = fragment.Descendants().FirstOrDefault(element => element.Name.LocalName == "AppxPackageInstallData");
            var isMainPackage = string.Equals(installData?.Attribute("MainPackage")?.Value, "true", StringComparison.OrdinalIgnoreCase);

            var identity = identities.TryGetValue(numericId, out var identityEntry) ? identityEntry : null;

            foreach (var file in fragment.Descendants().Where(element => element.Name.LocalName == "File"))
            {
                var identifier = file.Attribute("InstallerSpecificIdentifier")?.Value;
                if (string.IsNullOrWhiteSpace(identifier))
                {
                    // DynamicMetadata entries are block map side files, not packages.
                    continue;
                }

                var deliveryFileName = file.Attribute("FileName")?.Value ?? string.Empty;
                var digest = file.Attribute("Digest")?.Value;
                var size = long.TryParse(file.Attribute("Size")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSize)
                    ? parsedSize
                    : 0L;
                var sha256 = file.Descendants()
                    .FirstOrDefault(element =>
                        element.Name.LocalName == "AdditionalDigest"
                        && string.Equals(element.Attribute("Algorithm")?.Value, "SHA256", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                var readable = BuildReadableFileName(identifier, deliveryFileName);
                var parsedIdentifier = PackageIdentifier.TryParse(identifier, out var identifierParts) ? identifierParts : null;

                packages.Add(new WuPackage(
                    numericId,
                    identity?.UpdateId ?? string.Empty,
                    identity?.RevisionNumber ?? 0,
                    packageIdentityName,
                    deliveryFileName,
                    readable,
                    size,
                    digest,
                    sha256,
                    identifier,
                    parsedIdentifier?.Architecture,
                    parsedIdentifier?.Version,
                    isMainPackage,
                    isFramework,
                    identity?.HasSecuredFragment == true));
            }
        }

        return packages
            .GroupBy(package => package.InstallerSpecificIdentifier, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(package => package.HasFileUrl).First())
            .OrderByDescending(package => package.SizeInBytes)
            .ToList();
    }

    /// <summary>Rebuilds a human readable file name from the installer specific identifier.</summary>
    /// <param name="installerSpecificIdentifier">Identifier of the form <c>Name_Version_Arch__Publisher</c>.</param>
    /// <param name="deliveryFileName">File name served by the CDN, used only for its extension.</param>
    public static string BuildReadableFileName(string installerSpecificIdentifier, string deliveryFileName)
    {
        var extension = Path.GetExtension(deliveryFileName);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".appx";
        }

        if (!PackageIdentifier.TryParse(installerSpecificIdentifier, out var parts))
        {
            return installerSpecificIdentifier + extension;
        }

        var builder = new System.Text.StringBuilder(parts.Name);
        if (!string.IsNullOrWhiteSpace(parts.Version))
        {
            builder.Append('_').Append(parts.Version);
        }

        if (!string.IsNullOrWhiteSpace(parts.Architecture))
        {
            builder.Append('_').Append(parts.Architecture);
        }

        if (!string.IsNullOrWhiteSpace(parts.ResourceId))
        {
            builder.Append('_').Append(parts.ResourceId);
        }

        return builder.Append(extension).ToString();
    }

    private static Dictionary<string, UpdateIdentityInfo> CollectIdentities(XDocument document)
    {
        var map = new Dictionary<string, UpdateIdentityInfo>(StringComparer.Ordinal);
        var newUpdates = FindFirst(document, "NewUpdates");
        if (newUpdates is null)
        {
            return map;
        }

        foreach (var info in newUpdates.Descendants().Where(element => element.Name.LocalName == "UpdateInfo"))
        {
            var numericId = ChildValue(info, "ID");
            var xml = Child(info, "Xml");
            if (numericId is null || xml is null)
            {
                continue;
            }

            var fragment = ParseFragment(xml.Value);
            var identity = fragment.Descendants().FirstOrDefault(element => element.Name.LocalName == "UpdateIdentity");
            if (identity is null)
            {
                continue;
            }

            var revision = int.TryParse(identity.Attribute("RevisionNumber")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedRevision)
                ? parsedRevision
                : 0;
            var hasSecuredFragment = fragment.Descendants().Any(element => element.Name.LocalName == "SecuredFragment");

            map[numericId] = new UpdateIdentityInfo(identity.Attribute("UpdateID")?.Value, revision, hasSecuredFragment);
        }

        return map;
    }

    private static XElement ParseFragment(string fragmentText)
    {
        // Fragments are multi rooted, so they need a synthetic root before they can be parsed.
        try
        {
            return XElement.Parse("<fragment>" + fragmentText + "</fragment>", LoadOptions.PreserveWhitespace);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new Fe3Exception("The delivery service returned an unparsable update fragment.", ex);
        }
    }

    private static XElement? FindFirst(XContainer container, string localName) =>
        container.Descendants().FirstOrDefault(element => element.Name.LocalName == localName);

    private static XElement? Child(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName);

    private static string? ChildValue(XElement parent, string localName) => Child(parent, localName)?.Value;

    private sealed record UpdateIdentityInfo(string? UpdateId, int RevisionNumber, bool HasSecuredFragment);
}
