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
        var reference = arguments.Positionals.Count > 0 ? arguments.Positionals[0] : arguments.Get("dir");
        if (string.IsNullOrWhiteSpace(reference))
        {
            Console.Error.WriteLine("error: install needs a Store URL, a product id or a folder of packages (--dir)");
            return 2;
        }
        var architecture = WindowsArchitecture.Normalize(arguments.Get("arch") ?? WindowsArchitecture.Current);
        var locale = arguments.Get("locale") ?? Defaults.Locale;
        var directory = arguments.Get("dir");
        var dryRun = arguments.Has("dry-run");
        var json = arguments.Has("json");
        var fromFolderOnly = PackageFileLocator.LooksLikePath(reference) || Directory.Exists(reference);

        if (!OperatingSystem.IsWindows() && !dryRun)
        {
            Console.Error.WriteLine("error: installing needs Windows; use --dry-run to inspect the plan on this platform");
            return 1;
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
                .SaveAsync(directory, DownloadManifest.FromResults(resolution, downloaded, architecture, locale), CancellationToken.None)
                .ConfigureAwait(false);

            if (!json)
            {
                Console.WriteLine($"Downloaded {downloaded.Count} package(s) ({TablePrinter.FormatSize(downloaded.Sum(result => result.SizeInBytes))}).");
                Console.WriteLine();
            }
        }

        var files = PackageFileLocator.Resolve(directory);
        if (files.Count == 0)
        {
            Console.Error.WriteLine($"error: no package files in '{directory}'; run 'sluice download ... --dir {directory}' first");
            return 1;
        }

        var shell = new PowerShellHost();
        var inventory = new AppxInventory(shell);
        var planner = OperatingSystem.IsWindows() && shell.IsAvailable ? new InstallPlanner(inventory) : new InstallPlanner();
        var plan = await planner
            .CreatePlanAsync(files, new InstallPlannerOptions(architecture, locale), CancellationToken.None)
            .ConfigureAwait(false);

        if (!json)
        {
            WritePlan(plan, dryRun);
        }

        if (!plan.CanProceed)
        {
            Console.Error.WriteLine("error: the plan has blocking issues, nothing will be installed");
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

        if (!arguments.Has("yes") && !Console.IsInputRedirected && !Confirm())
        {
            Console.WriteLine("Cancelled.");
            return 1;
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

        if (json)
        {
            JsonOutput.Write(new
            {
                succeeded = outcome.Succeeded,
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

        WriteOutcome(outcome);

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

    private static void WriteOutcome(InstallOutcome outcome)
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

        Console.WriteLine();
        if (outcome.Registered is not null)
        {
            Console.WriteLine($"Registered: {outcome.Registered.PackageFullName} (status {outcome.Registered.Status})");
        }

        Console.WriteLine(outcome.Succeeded
            ? "Install finished successfully."
            : "Install did not finish: see the error explanation above.");
    }

    private static bool Confirm()
    {
        Console.Write("Install these packages now? [y/N] ");
        var answer = Console.ReadLine();
        return answer is not null && answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
    }
}
