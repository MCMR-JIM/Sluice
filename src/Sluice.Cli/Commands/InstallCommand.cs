using Sluice.Cli.Cli;
using Sluice.Download;
using Sluice.Install;
using Sluice.Packages;
using Sluice.Resolution;
using Sluice.Store;
using Sluice.Windows;

namespace Sluice.Cli.Commands;

/// <summary>
/// The one stop command: resolve, download, plan, install, verify. Also installs straight from
/// a folder that was downloaded earlier.
/// </summary>
internal static class InstallCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var arguments = CommandLineArguments.Parse(args);
        var json = arguments.Has("json");
        var reference = arguments.Positionals.Count > 0 ? arguments.Positionals[0] : arguments.Get("dir");
        if (string.IsNullOrWhiteSpace(reference))
        {
            return Program.Fail(
                json,
                "install needs a Store URL, a product id or a folder of packages (--dir)",
                "invalid-arguments",
                2);
        }

        var architecture = OptionValues.Architecture(arguments);
        var locale = OptionValues.Locale(arguments);
        var directory = arguments.Get("dir");
        var dryRun = arguments.Has("dry-run");
        var fromFolderOnly = PackageFileLocator.LooksLikePath(reference) || Directory.Exists(reference);

        if (!OperatingSystem.IsWindows() && !dryRun)
        {
            return Program.Fail(
                json,
                "installing needs Windows; use --dry-run to inspect the plan on this platform",
                "wrong-platform",
                1);
        }

        if (fromFolderOnly)
        {
            directory = reference;
        }

        directory ??= Path.Combine(Environment.CurrentDirectory, "downloads");
        PackageResolution? resolution = null;

        if (!fromFolderOnly && !arguments.Has("skip-download"))
        {
            using var resolver = new StorePackageResolver();
            resolution = await resolver.ResolveAsync(reference, locale, CancellationToken.None).ConfigureAwait(false);

            var selection = PackageSelector.Select(
                resolution,
                new PackageSelectionOptions(
                    architecture,
                    locale,
                    IncludeAllArchitectures: arguments.Has("all-arch")));

            if (!json)
            {
                Console.WriteLine($"Product : {resolution.Product.Title} ({resolution.Product.ProductId})");
                Console.WriteLine($"Target  : {architecture}, locale {locale}");
                Console.WriteLine($"Folder  : {directory}");
                Console.WriteLine($"App     : {selection.Main.Count} package(s) plus the dependencies its manifest asks for");
                Console.WriteLine();
            }

            if (dryRun)
            {
                // The manual says --dry-run touches nothing, and it used to download every package and
                // write the manifest before printing the plan. The plan can only be built from files on
                // disk, so a dry run from a Store reference reports the selection instead.
                WriteDryRun(resolution, selection, architecture, locale, directory!, arguments.Has("all-deps"), json);
                return 0;
            }

            using var downloader = new PackageDownloader();
            var downloadOptions = new DownloadOptions(MaxParallelism: arguments.GetInt32("parallel", 3));
            var downloaded = new List<DownloadResult>();

            if (arguments.Has("all-deps"))
            {
                // Offline archives want every candidate, not just the ones the manifest names.
                var wanted = new List<ResolvedPackage>(selection.Main);
                wanted.AddRange(selection.Dependencies);
                wanted.AddRange(selection.Resources);
                downloaded.AddRange(await downloader
                    .DownloadManyAsync(wanted, directory, downloadOptions, CancellationToken.None)
                    .ConfigureAwait(false));
            }
            else
            {
                var preparation = await new PackagePreparer(downloader)
                    .PrepareAsync(resolution, selection, directory, architecture, locale, downloadOptions, CancellationToken.None)
                    .ConfigureAwait(false);
                downloaded.AddRange(preparation.Downloaded);

                if (!json)
                {
                    foreach (var note in preparation.Notes)
                    {
                        Console.WriteLine($"  {note}");
                    }

                    Console.WriteLine();
                }
            }

            await DownloadManifest
                .SaveMergingAsync(directory, DownloadManifest.FromResults(resolution, downloaded, architecture, locale), CancellationToken.None)
                .ConfigureAwait(false);

            if (!json)
            {
                Console.WriteLine($"Downloaded {downloaded.Count} package(s) ({TablePrinter.FormatSize(downloaded.Sum(result => result.SizeInBytes))}).");
                Console.WriteLine();
            }
        }

        var found = PackageFileLocator.Resolve(directory);

        // A folder can hold more than one product's downloads. The manifest written when the packages
        // arrived says which of them belong together, so planning follows it instead of whatever happens
        // to be the biggest application package in the folder.
        var folderSelection = await DownloadManifest
            .SelectAsync(directory, found, CancellationToken.None)
            .ConfigureAwait(false);
        var files = folderSelection.Files;

        if (folderSelection.Missing.Count > 0 && !json)
        {
            Console.WriteLine(
                $"Note: {folderSelection.Missing.Count} package(s) named by the manifest are missing from "
                + $"'{directory}': {string.Join(", ", folderSelection.Missing)}");
            Console.WriteLine();
        }

        if (folderSelection.Ignored.Count > 0 && !json)
        {
            // A manifest entry that names a path rather than a file in this folder is not a missing
            // download: it is an entry that would have made the plan read a package from elsewhere.
            Console.WriteLine(
                $"Note: {folderSelection.Ignored.Count} manifest entry(s) name a path outside '{directory}' "
                + $"and were ignored: {string.Join(", ", folderSelection.Ignored)}");
            Console.WriteLine();
        }

        if (files.Count == 0)
        {
            return Program.Fail(
                json,
                $"no package files in '{directory}'; run 'sluice download ... --dir {directory}' first",
                "no-packages",
                1);
        }

        var shell = new PowerShellHost();
        var inventory = new AppxInventory(shell);

        // Planning asks about every dependency and then about the application; one snapshot answers all
        // of it from a single read of the machine. The installer keeps the live inventory, because it
        // changes the machine as it goes.
        var planner = OperatingSystem.IsWindows() && shell.IsAvailable
            ? new InstallPlanner(new SnapshotInventory(token => inventory.GetAllAsync(token)))
            : new InstallPlanner();
        var plan = await planner
            .CreatePlanAsync(files, new InstallPlannerOptions(architecture, locale), CancellationToken.None)
            .ConfigureAwait(false);

        if (!json)
        {
            WritePlan(plan, dryRun);
        }

        if (!plan.CanProceed)
        {
            // The blocking issues are the reason, so a JSON caller gets them instead of having to scrape
            // the plan table it never asked for.
            const string reason = "the plan has blocking issues, nothing will be installed";
            Console.Error.WriteLine($"error: {reason}");

            if (json)
            {
                JsonOutput.Write(new
                {
                    succeeded = false,
                    error = reason,
                    kind = "plan-blocked",
                    blockingIssues = plan.Issues
                        .Where(issue => issue.Severity == PlanIssueSeverity.Error)
                        .Select(issue => issue.Message.ToString()),
                });
            }

            return 1;
        }

        if (dryRun)
        {
            if (json)
            {
                JsonOutput.Write(new { dryRun = true, plan.MainPackageName, plan.MainVersion, plan.Architecture, steps = plan.Steps.Count });
            }
            else
            {
                Console.WriteLine("Dry run: nothing was installed.");
            }

            return 0;
        }

        var consent = DecideConsent(arguments.Has("yes"), Console.IsInputRedirected, Confirm);
        if (!consent.Proceed)
        {
            if (consent.Message is null)
            {
                Console.WriteLine("Cancelled.");

                if (json)
                {
                    JsonOutput.Write(new { succeeded = false, error = "cancelled", kind = "cancelled" });
                }

                return consent.ExitCode;
            }

            Console.Error.WriteLine(consent.Message);

            if (json)
            {
                // The message carries its own "error: " prefix for the terminal; the document carries the
                // reason itself.
                JsonOutput.Write(new
                {
                    succeeded = false,
                    error = consent.Message.Replace("error: ", string.Empty, StringComparison.Ordinal),
                    kind = "consent-required",
                });
            }

            return consent.ExitCode;
        }

        var installer = new AppxInstaller(
            shell,
            inventory,
            new AppxInstallerOptions(
                AllUsers: arguments.Has("all-users"),
                ForceShutdown: !arguments.Has("no-force-shutdown"),
                ForceUpdateFromAnyVersion: arguments.Has("force"),
                AllowRegisterFallback: !arguments.Has("no-register"),
                VerifyRegistration: !arguments.Has("no-verify")));

        var outcome = await installer.InstallAsync(plan, progress: null, CancellationToken.None).ConfigureAwait(false);
        var verificationRequested = !arguments.Has("no-verify");
        var verification = InstallVerdict.Verification(verificationRequested, outcome.VerificationProblem);

        if (json)
        {
            JsonOutput.Write(new
            {
                succeeded = outcome.Succeeded,
                notAttempted = plan.NotAttempted(outcome.StepResults).Select(step => new { kind = step.Kind.ToString(), file = step.FileName }),
                // True only when a check was asked for, the run finished and the registration was actually
                // read back. "Succeeded and no problem" was printed as verified, so "--no-verify" - which
                // reads nothing back - reported a verified install it never checked.
                verified = InstallVerdict.Verified(outcome.Succeeded, outcome.VerificationProblem, verificationRequested),
                // checked / unverified / skipped: three answers, because not checking is not a pass.
                verification,
                verificationProblem = outcome.VerificationProblem,
                registered = outcome.Registered?.PackageFullName,
                version = outcome.Registered?.Version,
                status = outcome.Registered?.Status,
                steps = outcome.StepResults.Select(result => new
                {
                    kind = result.Step.Kind.ToString(),
                    file = result.Step.FileName,
                    succeeded = result.Succeeded,
                    hresult = result.HResult is null ? null : Diagnostics.AppxErrorCatalog.Format(result.HResult.Value),
                    message = result.Message,
                }),
            });
            return outcome.Succeeded ? 0 : 1;
        }

        WriteOutcome(outcome, plan, verification);

        if (resolution is not null && outcome.Succeeded)
        {
            Console.WriteLine();
            Console.WriteLine($"Done: {resolution.Product.Title} is installed. Launch it from the Start menu, or run:");
            Console.WriteLine($"  explorer.exe shell:AppsFolder\\{resolution.Product.PackageFamilyName ?? plan.MainPackageName}!App");
        }

        return outcome.Succeeded ? 0 : 1;
    }

    private static void WritePlan(InstallPlan plan, bool dryRun)
    {
        Console.WriteLine($"Plan for {plan.MainPackageName ?? "unknown"} {plan.MainVersion} ({plan.Architecture})"
            + (dryRun ? " [dry run]" : string.Empty));
        Console.WriteLine();

        var ordered = plan.Steps.ToList();
        TablePrinter.Print(
            ["#", "KIND", "FILE"],
            ordered
                .Select((step, index) => (IReadOnlyList<string>)
                [
                    (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    step.Kind.ToString(),
                    step.FileName,
                ])
                .ToList());

        foreach (var issue in plan.Issues.Where(issue => issue.Severity != PlanIssueSeverity.Info))
        {
            Console.WriteLine();
            Console.WriteLine($"[{issue.Severity.ToString().ToLowerInvariant()}] {issue.Message}");
            if (issue.Remediation is not null)
            {
                Console.WriteLine($"         fix: {issue.Remediation}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Reports what a real run would fetch, without resolving file URLs or writing anything.
    /// </summary>
    /// <param name="resolution">Resolved product.</param>
    /// <param name="selection">Packages the selection picked.</param>
    /// <param name="architecture">Target architecture.</param>
    /// <param name="locale">Target locale.</param>
    /// <param name="directory">Folder a real run would download into.</param>
    /// <param name="allDependencies">True when every candidate dependency would be fetched.</param>
    /// <param name="json">True when the caller asked for JSON.</param>
    private static void WriteDryRun(
        PackageResolution resolution,
        PackageSelection selection,
        string architecture,
        string locale,
        string directory,
        bool allDependencies,
        bool json)
    {
        var candidates = new List<ResolvedPackage>(selection.Main);
        candidates.AddRange(selection.Dependencies);
        candidates.AddRange(selection.Resources);

        if (json)
        {
            JsonOutput.Write(new
            {
                dryRun = true,
                product = new { resolution.Product.ProductId, resolution.Product.Title },
                architecture,
                locale,
                directory,
                allDependencies,
                packages = candidates.Select(package => new
                {
                    file = package.Package.ReadableFileName,
                    architecture = package.Package.Architecture,
                    role = PackageRole.Of(package.Package),
                    size = package.Package.SizeInBytes,
                    downloadable = package.IsDownloadable,
                }),
            });

            return;
        }

        TablePrinter.Print(
            ["ROLE", "FILE", "ARCH", "SIZE"],
            candidates
                .Select(package => (IReadOnlyList<string>)
                [
                    PackageRole.Of(package.Package),
                    package.Package.ReadableFileName,
                    package.Package.Architecture ?? "neutral",
                    TablePrinter.FormatSize(package.Package.SizeInBytes),
                ])
                .ToList());

        Console.WriteLine();
        Console.WriteLine(
            $"Dry run: nothing was downloaded and nothing was written. A real run would fetch {candidates.Count} "
            + $"candidate package(s) into {directory} "
            + (allDependencies ? "(every candidate, because of --all-deps)" : "(only the dependencies the manifest declares)")
            + ", then plan and install them.");
    }

    /// <summary>
    /// Decides whether the install may go ahead. A run started by a script must say so explicitly:
    /// skipping the question because nobody can answer it installed packages without consent.
    /// </summary>
    /// <param name="assumeYes">True when the caller passed the yes switch.</param>
    /// <param name="stdinRedirected">True when standard input is not a terminal.</param>
    /// <param name="prompt">Asks the operator; only consulted when a terminal is attached.</param>
    /// <returns>Whether to proceed, the exit code to use otherwise, and a message for stderr.</returns>
    internal static (bool Proceed, int ExitCode, string? Message) DecideConsent(
        bool assumeYes,
        bool stdinRedirected,
        Func<bool> prompt)
    {
        if (assumeYes)
        {
            return (true, 0, null);
        }

        if (stdinRedirected)
        {
            return (
                false,
                2,
                "error: install asks for confirmation and standard input is not a terminal; pass --yes to install without a prompt");
        }

        return prompt() ? (true, 0, null) : (false, 1, null);
    }

    private static void WriteOutcome(InstallOutcome outcome, InstallPlan plan, string verification)
    {
        TablePrinter.Print(
            ["KIND", "FILE", "RESULT", "NOTE"],
            outcome.StepResults
                .Select(result => (IReadOnlyList<string>)
                [
                    result.Step.Kind.ToString(),
                    result.Step.FileName,
                    result.Succeeded ? "ok" : "failed",
                    result.HResult is null
                        ? result.Message ?? string.Empty
                        : Diagnostics.AppxErrorCatalog.Format(result.HResult.Value),
                ])
                .ToList());

        foreach (var failure in outcome.StepResults.Where(result => !result.Succeeded && result.Error is null))
        {
            Console.WriteLine();
            Console.WriteLine($"{failure.Step.Kind} failed: {failure.Message}");
        }

        foreach (var failure in outcome.StepResults.Where(result => !result.Succeeded && result.Error is not null))
        {
            Console.WriteLine();
            Console.WriteLine($"{failure.Error!.FormattedCode} {failure.Error.Name}");
            Console.WriteLine($"  what  : {failure.Error.Meaning}");
            Console.WriteLine($"  why   : {failure.Error.TypicalCause}");
            Console.WriteLine($"  fix   : {failure.Error.ManualSteps}");
            if (failure.HResult is not null)
            {
                Console.WriteLine($"  detail: {failure.Message}");
            }
        }

        var notAttempted = plan.NotAttempted(outcome.StepResults);
        if (notAttempted.Count > 0)
        {
            // The run stops at the first failure. Saying what it never reached is the difference between
            // "still working" and "gave up here", and a script can see it in the JSON too.
            Console.WriteLine();
            Console.WriteLine($"Not attempted ({notAttempted.Count}):");
            foreach (var step in notAttempted)
            {
                Console.WriteLine($"  {step.Kind} {step.FileName}");
            }
        }

        Console.WriteLine();
        if (outcome.Registered is not null)
        {
            Console.WriteLine($"Registered: {outcome.Registered.PackageFullName} (status {outcome.Registered.Status})");
        }

        if (outcome.VerificationProblem is { } problem)
        {
            Console.WriteLine($"Unverified: {problem}");
        }

        // A skipped check is not a pass, and the run used to say nothing at all about it.
        var skippedCheck = string.Equals(verification, "skipped", StringComparison.Ordinal);
        if (skippedCheck)
        {
            Console.WriteLine("Verification: skipped (--no-verify), so nothing was read back.");
        }

        Console.WriteLine(outcome.Succeeded
            ? skippedCheck
                ? "Install finished. The registration was not checked."
                : outcome.VerificationProblem is null
                    ? "Install finished successfully."
                    : "Install finished, but the registration could not be verified."
            : "Install did not finish: see the error explanation above.");
    }

    private static bool Confirm()
    {
        Console.Write("Install these packages now? [y/N] ");
        var answer = Console.ReadLine();
        return answer is not null && answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
    }
}
