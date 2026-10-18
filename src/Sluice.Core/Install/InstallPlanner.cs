using Sluice.Packages;
using Sluice.Windows;

namespace Sluice.Install;

/// <summary>Inputs that influence how a plan is built.</summary>
/// <param name="Architecture">Target architecture; the machine's own when omitted.</param>
/// <param name="Locale">Locale used to pick resource packages.</param>
public sealed record InstallPlannerOptions(string? Architecture = null, string? Locale = null);

/// <summary>
/// Builds the install plan for a folder of downloaded packages: which framework packages have
/// to go in first, in what order, and what is missing.
/// </summary>
public sealed class InstallPlanner
{
    private readonly IInstalledPackageInventory? _inventory;

    /// <summary>Creates a planner that does not inspect the machine.</summary>
    public InstallPlanner()
    {
    }

    /// <summary>Creates a planner that compares against what is already installed.</summary>
    /// <param name="inventory">Inventory to use.</param>
    public InstallPlanner(IInstalledPackageInventory inventory) => _inventory = inventory;

    /// <summary>Creates a planner over a PowerShell host.</summary>
    /// <param name="shell">Host used to query installed packages.</param>
    public InstallPlanner(PowerShellHost shell) : this(new AppxInventory(shell))
    {
    }

    /// <summary>Builds a plan for the packages found in <paramref name="packageFiles"/>.</summary>
    /// <param name="packageFiles">Candidate package files, in any order.</param>
    /// <param name="options">Planning options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<InstallPlan> CreatePlanAsync(
        IEnumerable<string> packageFiles,
        InstallPlannerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageFiles);

        options ??= new InstallPlannerOptions();
        var targetArchitecture = WindowsArchitecture.Normalize(options.Architecture ?? WindowsArchitecture.Current);
        var issues = new List<PlanIssue>();
        var steps = new List<InstallStep>();

        var analyses = new List<PackageAnalysis>();
        foreach (var file in packageFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var analysis = PackageAnalyzer.TryAnalyze(file);
            if (analysis is null)
            {
                issues.Add(new PlanIssue(
                    PlanIssueSeverity.Warning,
                    Diagnostics.DiagnosticMessage.Of("PlanSkippedFile", Path.GetFileName(file))));
                continue;
            }

            analyses.Add(analysis);
        }

        var application = analyses
            .Where(analysis => analysis.Kind is PackageKind.Bundle or PackageKind.App)
            .OrderByDescending(analysis => analysis.SizeInBytes)
            .FirstOrDefault();

        if (application is null)
        {
            throw new SluiceException(
                "No application package ('.msix', '.appx', '.msixbundle' or '.appxbundle') was found in the given folder, "
                + "so there is nothing to install.");
        }

        var mainIdentity = application.Identity;
        var dependencies = application.Dependencies;

        var locale = string.IsNullOrWhiteSpace(options.Locale)
            ? System.Globalization.CultureInfo.CurrentUICulture.Name
            : options.Locale;

        if (application.Kind == PackageKind.Bundle)
        {
            var selection = BundlePackageSelector.Select(application, targetArchitecture, string.IsNullOrWhiteSpace(locale) ? null : [locale]);
            foreach (var note in selection.Notes)
            {
                issues.Add(new PlanIssue(PlanIssueSeverity.Info, note));
            }

            if (selection.Application is null)
            {
                issues.Add(new PlanIssue(
                    PlanIssueSeverity.Error,
                    Diagnostics.DiagnosticMessage.Of("PlanBundleNoApplication", application.FileName, targetArchitecture),
                    Diagnostics.DiagnosticMessage.Of("PlanBundleNoApplicationHint")));
            }
            else
            {
                try
                {
                    var child = PackageAnalyzer.AnalyzeChild(application.FilePath, selection.Application.FileName);
                    mainIdentity = child.Identity ?? mainIdentity;
                    dependencies = child.Dependencies;
                }
                catch (SluiceException ex)
                {
                    issues.Add(new PlanIssue(
                        PlanIssueSeverity.Warning,
                        Diagnostics.DiagnosticMessage.Of("PlanBundleManifestUnreadable", ex.Message)));
                }
            }
        }

        var installed = await ReadInventoryAsync(issues, cancellationToken).ConfigureAwait(false);
        var candidates = analyses
            .Where(analysis => !ReferenceEquals(analysis, application))
            .Where(analysis => analysis.Identity is not null)
            .ToList();

        foreach (var dependency in dependencies)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var requiredVersion = ParseVersion(dependency.MinVersion);
            var installedMatch = installed?
                .Where(package => NamesMatch(package.Name, dependency.Name))
                .OrderByDescending(package => package.GetVersion())
                .FirstOrDefault();

            var matches = candidates
                .Where(analysis => NamesMatch(analysis.Identity!.Name, dependency.Name))
                .OrderByDescending(analysis => WindowsArchitecture.MatchScore(analysis.Identity!.Architecture, targetArchitecture))
                .ThenByDescending(analysis => ParseVersion(analysis.Identity!.Version))
                .ToList();

            if (matches.Count == 0)
            {
                if (installedMatch is not null && Satisfies(installedMatch, requiredVersion))
                {
                    issues.Add(new PlanIssue(
                        PlanIssueSeverity.Info,
                        Diagnostics.DiagnosticMessage.Of("PlanDependencyInstalled", dependency.Name, installedMatch.Version)));
                    continue;
                }

                issues.Add(new PlanIssue(
                    PlanIssueSeverity.Error,
                    Diagnostics.DiagnosticMessage.Of(
                        "PlanMissingDependency", dependency, mainIdentity?.Name ?? application.FileName),
                    Diagnostics.DiagnosticMessage.Of("PlanMissingDependencyHint")));
                continue;
            }

            var chosen = matches[0];
            var chosenVersion = ParseVersion(chosen.Identity!.Version);
            if (installedMatch is not null
                && Satisfies(installedMatch, requiredVersion)
                && (chosenVersion is null || installedMatch.GetVersion() >= chosenVersion))
            {
                issues.Add(new PlanIssue(
                    PlanIssueSeverity.Info,
                    Diagnostics.DiagnosticMessage.Of(
                        "PlanDependencySkipped", chosen.Identity.Name, installedMatch.Version, chosen.FileName)));
                continue;
            }

            steps.Add(new InstallStep(
                InstallStepKind.InstallFramework,
                chosen.FilePath,
                chosen.Identity.Name,
                chosen.Identity.Version,
                chosen.Identity.Architecture,
                Diagnostics.DiagnosticMessage.Of(
                    "PlanStepFramework", mainIdentity?.Name ?? application.FileName, dependency)));
        }

        var resources = candidates
            .Where(analysis => analysis.Kind == PackageKind.Resource)
            .Where(analysis => string.IsNullOrWhiteSpace(locale)
                || string.Equals(analysis.Identity!.ResourceId?.Split('-')[0], locale.Split('-')[0], StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Files that were already accounted for (planned or deliberately skipped) should not be
        // reported again as unused.
        var considered = steps
            .Select(step => step.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Candidates that exist for a declared dependency were evaluated, even when an installed
        // version made installing them unnecessary.
        var declaredNames = dependencies.Select(dependency => dependency.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var evaluated in candidates.Where(analysis => analysis.Identity is not null && declaredNames.Contains(analysis.Identity.Name)))
        {
            considered.Add(evaluated.FilePath);
        }

        foreach (var unused in candidates.Where(analysis => analysis.Kind != PackageKind.Resource
            && !considered.Contains(analysis.FilePath)))
        {
            issues.Add(new PlanIssue(
                PlanIssueSeverity.Info,
                Diagnostics.DiagnosticMessage.Of("PlanUnusedFile", unused.FileName)));
        }

        var mainInstalled = installed is not null && mainIdentity is not null
            ? await FindInstalledAsync(mainIdentity.Name, issues, cancellationToken).ConfigureAwait(false)
            : null;

        var mainVersion = ParseVersion(mainIdentity?.Version);
        if (mainInstalled is not null && mainVersion is not null && mainInstalled.GetVersion() >= mainVersion)
        {
            issues.Add(new PlanIssue(
                PlanIssueSeverity.Info,
                Diagnostics.DiagnosticMessage.Of("PlanMainAlreadyInstalled", mainIdentity!.Name, mainInstalled.Version)));
        }

        steps.Add(new InstallStep(
            InstallStepKind.InstallMain,
            application.FilePath,
            mainIdentity?.Name,
            mainIdentity?.Version,
            mainIdentity?.Architecture,
            Diagnostics.DiagnosticMessage.Of(
                application.Kind == PackageKind.Bundle ? "PlanStepMainBundle" : "PlanStepMain")));

        foreach (var resource in resources)
        {
            steps.Add(new InstallStep(
                InstallStepKind.InstallResource,
                resource.FilePath,
                resource.Identity!.Name,
                resource.Identity.Version,
                resource.Identity.Architecture,
                Diagnostics.DiagnosticMessage.Of("PlanStepResource", resource.Identity.ResourceId)));
        }

        if (mainIdentity is not null)
        {
            steps.Add(new InstallStep(
                InstallStepKind.Verify,
                application.FilePath,
                mainIdentity.Name,
                mainIdentity.Version,
                mainIdentity.Architecture,
                Diagnostics.DiagnosticMessage.Of("PlanStepVerify")));
        }

        return new InstallPlan(mainIdentity?.Name, mainIdentity?.Version, targetArchitecture, steps, issues);
    }

    private async Task<IReadOnlyList<InstalledPackage>?> ReadInventoryAsync(List<PlanIssue> issues, CancellationToken cancellationToken)
    {
        if (_inventory is null)
        {
            return null;
        }

        try
        {
            return await _inventory.GetFrameworksAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PowerShellUnavailableException ex)
        {
            issues.Add(new PlanIssue(
                PlanIssueSeverity.Warning,
                Diagnostics.DiagnosticMessage.Of("PlanInventoryUnreadable", ex.Message)));
            return null;
        }
    }

    private async Task<InstalledPackage?> FindInstalledAsync(string name, List<PlanIssue> issues, CancellationToken cancellationToken)
    {
        if (_inventory is null)
        {
            return null;
        }

        try
        {
            return await _inventory.FindHighestAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (PowerShellUnavailableException ex)
        {
            issues.Add(new PlanIssue(
                PlanIssueSeverity.Warning,
                Diagnostics.DiagnosticMessage.Of("PlanInventoryCheckFailed", name, ex.Message)));
            return null;
        }
    }

    private static bool NamesMatch(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static Version? ParseVersion(string? value) =>
        System.Version.TryParse(value, out var parsed) ? parsed : null;

    private static bool Satisfies(InstalledPackage installed, Version? required) =>
        required is null || installed.GetVersion() >= required;
}
