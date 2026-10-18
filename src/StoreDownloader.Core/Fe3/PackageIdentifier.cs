using System.Globalization;
using System.Text.RegularExpressions;

namespace StoreDownloader.Fe3;

/// <summary>
/// Parsed parts of a delivery service installer specific identifier.
/// </summary>
/// <remarks>
/// The delivery service uses the package full name layout without the trailing architecture
/// restrictions: <c>Name_Version_Architecture_ResourceId_PublisherId</c>, where the resource id
/// is empty for packages that have none. Because package names may contain dots and
/// underscores, the version is located by pattern instead of by counting separators.
/// </remarks>
/// <param name="Name">Package name, for example <c>5319275A.WhatsAppDesktop</c>.</param>
/// <param name="Version">Package version, for example <c>2.2640.100.0</c>.</param>
/// <param name="Architecture">Architecture, for example <c>x64</c> or <c>neutral</c>.</param>
/// <param name="ResourceId">Resource id, or <c>null</c> when the package has none.</param>
/// <param name="PublisherId">Publisher hash, for example <c>8wekyb3d8bbwe</c>.</param>
public sealed partial record PackageIdentifier(
    string Name,
    string? Version,
    string? Architecture,
    string? ResourceId,
    string? PublisherId)
{
    [GeneratedRegex(@"^\d+(\.\d+){1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    /// <summary>
    /// Parses an installer specific identifier.
    /// </summary>
    /// <param name="value">Identifier to parse.</param>
    /// <param name="identifier">Parsed identifier.</param>
    /// <returns><c>true</c> when a package name could be extracted.</returns>
    public static bool TryParse(string? value, out PackageIdentifier identifier)
    {
        identifier = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Trim().Split('_');
        var versionIndex = -1;
        for (var index = 1; index < parts.Length; index++)
        {
            if (VersionPattern().IsMatch(parts[index]))
            {
                versionIndex = index;
                break;
            }
        }

        if (versionIndex <= 0)
        {
            // No version segment: treat the whole value as a bare package name.
            identifier = new PackageIdentifier(value.Trim(), null, null, null, null);
            return true;
        }

        var name = string.Join('_', parts[..versionIndex]);
        var version = parts[versionIndex];
        var architecture = versionIndex + 1 < parts.Length ? parts[versionIndex + 1] : null;
        var tail = parts.Skip(versionIndex + 2).Where(part => part.Length > 0).ToList();

        string? resourceId = null;
        string? publisherId = null;
        if (tail.Count >= 2)
        {
            resourceId = tail[0];
            publisherId = tail[^1];
        }
        else if (tail.Count == 1)
        {
            publisherId = tail[0];
        }

        identifier = new PackageIdentifier(
            name,
            version,
            string.IsNullOrWhiteSpace(architecture) || architecture == "~" ? null : architecture.ToLowerInvariant(),
            string.IsNullOrWhiteSpace(resourceId) || resourceId == "~" ? null : resourceId,
            publisherId);

        return !string.IsNullOrWhiteSpace(name);
    }

    /// <summary>Version as a comparable tuple, empty when the identifier carries no version.</summary>
    public Version? ToVersion()
    {
        if (string.IsNullOrWhiteSpace(Version))
        {
            return null;
        }

        var numbers = Version.Split('.').Select(part =>
            int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0).ToArray();

        return numbers.Length switch
        {
            1 => new Version(numbers[0], 0),
            2 => new Version(numbers[0], numbers[1]),
            3 => new Version(numbers[0], numbers[1], numbers[2]),
            _ => new Version(numbers[0], numbers[1], numbers[2], numbers[3]),
        };
    }
}
