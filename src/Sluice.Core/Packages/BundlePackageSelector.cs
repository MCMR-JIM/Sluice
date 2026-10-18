namespace Sluice.Packages;

/// <summary>The parts of a bundle that should be installed on a given machine.</summary>
/// <param name="Application">Application package chosen for the target architecture.</param>
/// <param name="Resources">Resource packages matching the target languages.</param>
/// <param name="Notes">Human readable explanation of the choice.</param>
public sealed record BundleSelection(
    BundleChildPackage? Application,
    IReadOnlyList<BundleChildPackage> Resources,
    IReadOnlyList<Diagnostics.DiagnosticMessage> Notes);

/// <summary>
/// Chooses which child packages of a bundle apply to a machine.
/// </summary>
/// <remarks>
/// Windows performs this selection itself when a bundle is installed with
/// <c>Add-AppxPackage</c>. It only has to be done here when packages are installed
/// individually, for example when a bundle has been unpacked by hand.
/// </remarks>
public static class BundlePackageSelector
{
    /// <summary>Selects the application and resource packages for a target machine.</summary>
    /// <param name="analysis">Analysis of a bundle.</param>
    /// <param name="targetArchitecture">Machine architecture, for example <c>x64</c>.</param>
    /// <param name="preferredLocales">Locales to keep resource packages for.</param>
    public static BundleSelection Select(
        PackageAnalysis analysis,
        string targetArchitecture,
        IEnumerable<string>? preferredLocales = null)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var notes = new List<Diagnostics.DiagnosticMessage>();
        var applications = analysis.Children.Where(child => child.IsApplication).ToList();
        if (applications.Count == 0)
        {
            notes.Add(Diagnostics.DiagnosticMessage.Of("BundleNoApplication"));
        }

        var application = applications
            .OrderByDescending(child => WindowsArchitecture.MatchScore(child.Architecture, targetArchitecture))
            .ThenByDescending(child => child.SizeInBytes)
            .FirstOrDefault();

        if (application is not null)
        {
            notes.Add(Diagnostics.DiagnosticMessage.Of("BundleSelected", application.FileName, targetArchitecture));
        }

        var locales = (preferredLocales ?? [])
            .Where(locale => !string.IsNullOrWhiteSpace(locale))
            .Select(locale => locale.Split('-')[0].ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var resources = analysis.Children
            .Where(child => child.IsResource)
            .Where(child => MatchesLocale(child.ResourceId, locales))
            .Where(child => WindowsArchitecture.IsCompatible(child.Architecture, targetArchitecture))
            .ToList();

        if (resources.Count > 0)
        {
            notes.Add(Diagnostics.DiagnosticMessage.Of("BundleKeptResources", resources.Count, string.Join(", ", locales)));
        }

        return new BundleSelection(application, resources, notes);
    }

    private static bool MatchesLocale(string? resourceId, HashSet<string> locales)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return false;
        }

        // Resource ids inside bundles are either a language tag or a qualifier such as scale-100.
        var qualifier = resourceId.Split(',')[0].Trim();
        if (qualifier.StartsWith("scale-", StringComparison.OrdinalIgnoreCase)
            || qualifier.StartsWith("contrast-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (locales.Count == 0)
        {
            return true;
        }

        return locales.Contains(qualifier.Split('-')[0].ToLowerInvariant());
    }
}
