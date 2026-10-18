using Sluice.Download;
using Sluice.Packages;
using Sluice.Resolution;

namespace Sluice.Install;

/// <summary>What a preparation run downloaded, and why.</summary>
/// <param name="Downloaded">Files that ended up on disk.</param>
/// <param name="RequiredDependencies">Dependencies the application actually declares.</param>
/// <param name="Notes">Human readable notes about what was skipped and why.</param>
public sealed record PreparationResult(
    IReadOnlyList<DownloadResult> Downloaded,
    IReadOnlyList<PackageDependency> RequiredDependencies,
    IReadOnlyList<Diagnostics.DiagnosticMessage> Notes);

/// <summary>
/// Downloads the application first, reads the dependencies out of its manifest, and only then
/// fetches the framework packages that the manifest actually names.
/// </summary>
/// <remarks>
/// Store categories advertise framework packages for every architecture and every generation of
/// the runtime. Downloading them all wastes hundreds of megabytes for a 20 MiB app, so the
/// manifest decides instead of the catalog.
/// </remarks>
public sealed class PackagePreparer(PackageDownloader downloader)
{
    /// <summary>Prepares a download folder for an install.</summary>
    /// <param name="resolution">Resolution of the product.</param>
    /// <param name="selection">Selection produced by <see cref="PackageSelector"/>.</param>
    /// <param name="directory">Destination folder.</param>
    /// <param name="architecture">Target architecture.</param>
    /// <param name="locale">Target locale.</param>
    /// <param name="options">Download options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<PreparationResult> PrepareAsync(
        PackageResolution resolution,
        PackageSelection selection,
        string directory,
        string architecture,
        string locale,
        DownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(selection);

        var notes = new List<Diagnostics.DiagnosticMessage>();
        var downloaded = new List<DownloadResult>();

        var mainResults = await downloader
            .DownloadManyAsync(selection.Main, directory, options, cancellationToken)
            .ConfigureAwait(false);
        downloaded.AddRange(mainResults);

        var dependencies = ReadDependencies(mainResults, architecture, locale, notes);
        var required = SelectRequired(selection, dependencies, locale, notes);

        if (required.Count > 0)
        {
            var dependencyResults = await downloader
                .DownloadManyAsync(required, directory, options, cancellationToken)
                .ConfigureAwait(false);
            downloaded.AddRange(dependencyResults);
        }

        return new PreparationResult(downloaded, dependencies, notes);
    }

    /// <summary>
    /// Picks the candidate packages whose identity name matches a dependency the application
    /// declared, keeping resource packages for the requested locale.
    /// </summary>
    /// <param name="selection">Selection produced by <see cref="PackageSelector"/>.</param>
    /// <param name="dependencies">Dependencies read from the application manifest.</param>
    /// <param name="locale">Target locale.</param>
    /// <param name="notes">Collects explanations of what was left out.</param>
    public static IReadOnlyList<ResolvedPackage> SelectRequired(
        PackageSelection selection,
        IReadOnlyList<PackageDependency> dependencies,
        string locale,
        List<Diagnostics.DiagnosticMessage>? notes = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(dependencies);

        var requiredNames = dependencies
            .Select(dependency => dependency.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var required = new List<ResolvedPackage>();
        foreach (var candidate in selection.Dependencies)
        {
            var name = candidate.Package.PackageIdentityName;
            if (name is null)
            {
                continue;
            }

            if (requiredNames.Contains(name))
            {
                required.Add(candidate);
                continue;
            }

            notes?.Add(Diagnostics.DiagnosticMessage.Of(
                    "PrepareNotDeclared", candidate.Package.ReadableFileName));
        }

        // Resource packages only make sense for the app itself, never for a framework.
        foreach (var resource in selection.Resources)
        {
            var mainName = selection.Main.Count > 0 ? selection.Main[0].Package.PackageIdentityName : null;
            if (string.Equals(resource.Package.PackageIdentityName, mainName, StringComparison.OrdinalIgnoreCase))
            {
                required.Add(resource);
                continue;
            }

            notes?.Add($"{resource.Package.ReadableFileName} belongs to another package family; not downloading it.");
        }

        return required;
    }

    private static IReadOnlyList<PackageDependency> ReadDependencies(
        IReadOnlyList<DownloadResult> mainResults,
        string architecture,
        string locale,
        List<Diagnostics.DiagnosticMessage> notes)
    {
        foreach (var result in mainResults)
        {
            var analysis = PackageAnalyzer.TryAnalyze(result.Path);
            if (analysis is null)
            {
                continue;
            }

            if (analysis.Kind != PackageKind.Bundle)
            {
                return analysis.Dependencies;
            }

            var bundleSelection = BundlePackageSelector.Select(analysis, architecture, [locale]);
            if (bundleSelection.Application is null)
            {
                notes.Add(Diagnostics.DiagnosticMessage.Of("PrepareNoApplication", analysis.FileName, architecture));
                return [];
            }

            try
            {
                var child = PackageAnalyzer.AnalyzeChild(analysis.FilePath, bundleSelection.Application.FileName);
                notes.Add(Diagnostics.DiagnosticMessage.Of(
                "PrepareInnerPackage", analysis.FileName, bundleSelection.Application.FileName, architecture));
                return child.Dependencies;
            }
            catch (SluiceException ex)
            {
                notes.Add(Diagnostics.DiagnosticMessage.Of("PrepareUnreadableBundle", analysis.FileName, ex.Message));
                return [];
            }
        }

        return [];
    }
}
