using StoreDownloader.Fe3;
using StoreDownloader.Resolution;

namespace StoreDownloader.Packages;

/// <summary>How packages should be picked out of a resolution.</summary>
/// <param name="Architecture">Target architecture, for example <c>x64</c>.</param>
/// <param name="Locale">Target locale used for resource packages.</param>
/// <param name="IncludeAllArchitectures">Keep every architecture instead of only compatible ones.</param>
/// <param name="IncludeUnresolvedUrls">Keep packages that have no signed URL.</param>
public sealed record PackageSelectionOptions(
    string Architecture,
    string Locale,
    bool IncludeAllArchitectures = false,
    bool IncludeUnresolvedUrls = false);

/// <summary>The packages that make up one installation.</summary>
/// <param name="Main">Main application packages (usually a single bundle).</param>
/// <param name="Dependencies">Framework packages the application needs.</param>
/// <param name="Resources">Resource packages for the target locales.</param>
/// <param name="Skipped">Packages that were deliberately left out, with reasons.</param>
public sealed record PackageSelection(
    IReadOnlyList<ResolvedPackage> Main,
    IReadOnlyList<ResolvedPackage> Dependencies,
    IReadOnlyList<ResolvedPackage> Resources,
    IReadOnlyList<string> Skipped)
{
    /// <summary>Every selected package, main first.</summary>
    public IReadOnlyList<ResolvedPackage> All => [.. Main, .. Dependencies, .. Resources];

    /// <summary>Total size of the selection.</summary>
    public long TotalSizeInBytes => All.Sum(package => package.Package.SizeInBytes);
}

/// <summary>
/// Picks what to download for a product: the newest main package for the target
/// architecture, the newest compatible framework package per dependency, and the resource
/// packages for the requested locale.
/// </summary>
public static class PackageSelector
{
    /// <summary>Selects packages from a resolution.</summary>
    /// <param name="resolution">Resolution of a Store product.</param>
    /// <param name="options">Selection options.</param>
    public static PackageSelection Select(PackageResolution resolution, PackageSelectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(options);

        var targetArchitecture = WindowsArchitecture.Normalize(options.Architecture);
        var skipped = new List<string>();

        var candidates = resolution.Packages
            .Where(package => options.IncludeUnresolvedUrls || package.IsDownloadable)
            .Where(package => options.IncludeAllArchitectures || WindowsArchitecture.IsCompatible(package.Package.Architecture, targetArchitecture))
            .ToList();

        foreach (var package in resolution.Packages.Except(candidates))
        {
            skipped.Add(package.IsDownloadable
                ? $"{package.Package.ReadableFileName}: not compatible with {targetArchitecture}"
                : $"{package.Package.ReadableFileName}: no download url available");
        }

        var mainIdentityName = MainIdentityName(resolution);

        var resources = candidates
            .Where(package => Parse(package.Package).ResourceId is not null)
            .Where(package => MatchesLocale(Parse(package.Package).ResourceId, options.Locale))
            .OrderBy(package => package.Package.ReadableFileName, StringComparer.Ordinal)
            .ToList();

        var nonResource = candidates.Where(package => Parse(package.Package).ResourceId is null).ToList();

        var mainCandidates = nonResource
            .Where(package => mainIdentityName is not null
                && string.Equals(package.Package.PackageIdentityName, mainIdentityName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (mainCandidates.Count == 0)
        {
            // Fall back to the largest package: on the Store the app is always the heavy one.
            mainCandidates = nonResource
                .OrderByDescending(package => package.Package.SizeInBytes)
                .Take(1)
                .ToList();
        }

        var mainSelection = PickBest(mainCandidates, targetArchitecture, allArchitectures: false);
        var mainIdentifiers = mainSelection
            .Select(package => package.Package.InstallerSpecificIdentifier)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Older builds of the application itself are not dependencies, they are simply stale.
        var dependencyCandidates = nonResource
            .Where(package => !mainIdentifiers.Contains(package.Package.InstallerSpecificIdentifier))
            .Where(package => mainIdentityName is null
                || !string.Equals(package.Package.PackageIdentityName, mainIdentityName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var dependencySelection = new List<ResolvedPackage>();
        foreach (var group in dependencyCandidates
            .GroupBy(package => package.Package.PackageIdentityName ?? package.Package.ReadableFileName, StringComparer.OrdinalIgnoreCase))
        {
            dependencySelection.AddRange(PickBest([.. group], targetArchitecture, options.IncludeAllArchitectures));
        }

        return new PackageSelection(mainSelection, dependencySelection, resources, skipped);
    }

    private static List<ResolvedPackage> PickBest(
        List<ResolvedPackage> candidates,
        string targetArchitecture,
        bool allArchitectures)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        if (allArchitectures)
        {
            // Offline archives keep one copy per architecture so they can be replayed elsewhere.
            return candidates
                .GroupBy(package => WindowsArchitecture.Normalize(package.Package.Architecture), StringComparer.Ordinal)
                .Select(group => group
                    .OrderByDescending(package => Parse(package.Package).ToVersion())
                    .ThenByDescending(package => package.Package.SizeInBytes)
                    .First())
                .ToList();
        }

        var best = candidates
            .OrderByDescending(package => WindowsArchitecture.MatchScore(package.Package.Architecture, targetArchitecture))
            .ThenByDescending(package => Parse(package.Package).ToVersion())
            .ThenByDescending(package => package.Package.SizeInBytes)
            .First();

        return [best];
    }

    private static string? MainIdentityName(PackageResolution resolution)
    {
        var familyName = resolution.Product.PackageFamilyName;
        if (string.IsNullOrWhiteSpace(familyName))
        {
            return null;
        }

        var separator = familyName.IndexOf('_', StringComparison.Ordinal);
        return separator > 0 ? familyName[..separator] : familyName;
    }

    private static bool MatchesLocale(string? resourceId, string locale)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || string.IsNullOrWhiteSpace(locale))
        {
            return true;
        }

        return string.Equals(resourceId.Split('-')[0], locale.Split('-')[0], StringComparison.OrdinalIgnoreCase);
    }

    private static PackageIdentifier Parse(WuPackage package) =>
        PackageIdentifier.TryParse(package.InstallerSpecificIdentifier, out var parsed)
            ? parsed
            : new PackageIdentifier(package.PackageIdentityName ?? package.ReadableFileName, null, null, null, null);
}
