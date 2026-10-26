using Sluice.Cli.Cli;
using Sluice.Download;
using Sluice.Install;
using Sluice.Packages;
using Sluice.Windows;

namespace Sluice.Cli.Commands;

/// <summary>Shows the install plan for downloaded packages without touching the machine.</summary>
internal static class PlanCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var arguments = CommandLineArguments.Parse(args);
        if (arguments.Positionals.Count == 0)
        {
            return Program.Fail(
                arguments.Has("json"),
                "plan needs a folder of downloaded packages",
                "invalid-arguments",
                2);
        }

        var folder = arguments.Positionals[0];
        var found = PackageFileLocator.Resolve(folder);
        if (found.Count == 0)
        {
            return Program.Fail(
                arguments.Has("json"),
                $"no package files found in '{folder}'",
                "no-packages",
                1);
        }

        // Same rule as install: a manifest in the folder says which packages belong to one product.
        var folderSelection = await DownloadManifest
            .SelectAsync(Path.GetDirectoryName(Path.GetFullPath(found[0])) ?? folder, found, CancellationToken.None)
            .ConfigureAwait(false);
        var files = folderSelection.Files;
        if (folderSelection.Missing.Count > 0 && !arguments.Has("json"))
        {
            Console.WriteLine(
                $"Note: {folderSelection.Missing.Count} package(s) named by the manifest are missing: "
                + string.Join(", ", folderSelection.Missing));
            Console.WriteLine();
        }

        var shell = new PowerShellHost();
        var inventory = new AppxInventory(shell);
        var planner = OperatingSystem.IsWindows() && shell.IsAvailable
            ? new InstallPlanner(new SnapshotInventory(token => inventory.GetAllAsync(token)))
            : new InstallPlanner();

        var plan = await planner.CreatePlanAsync(
            files,
            new InstallPlannerOptions(
                OptionValues.Architecture(arguments),
                OptionValues.Locale(arguments)),
            CancellationToken.None).ConfigureAwait(false);

        if (arguments.Has("json"))
        {
            JsonOutput.Write(new
            {
                mainPackage = plan.MainPackageName,
                mainVersion = plan.MainVersion,
                architecture = plan.Architecture,
                canProceed = plan.CanProceed,
                steps = plan.Steps.Select(step => new
                {
                    order = plan.Steps.ToList().IndexOf(step) + 1,
                    kind = step.Kind.ToString(),
                    file = step.FileName,
                    package = step.PackageName,
                    version = step.Version,
                    reason = step.Reason,
                }),
                issues = plan.Issues.Select(issue => new
                {
                    severity = issue.Severity.ToString(),
                    issue.Message,
                    issue.Remediation,
                }),
            });
            return plan.CanProceed ? 0 : 1;
        }

        Console.WriteLine($"Main package : {plan.MainPackageName ?? "unknown"} {plan.MainVersion}".TrimEnd());
        Console.WriteLine($"Architecture : {plan.Architecture}");
        Console.WriteLine($"Installable  : {(plan.CanProceed ? "yes" : "no, see the blocking issues below")}");
        Console.WriteLine();

        var ordered = plan.Steps.ToList();
        TablePrinter.Print(
            ["#", "KIND", "FILE", "WHY"],
            ordered
                .Select((step, index) => (IReadOnlyList<string>)
                [
                    (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    step.Kind.ToString(),
                    step.FileName,
                    step.Reason.Text,
                ])
                .ToList());

        if (plan.Issues.Count > 0)
        {
            Console.WriteLine();
            foreach (var issue in plan.Issues)
            {
                Console.WriteLine($"[{issue.Severity.ToString().ToLowerInvariant()}] {issue.Message.Text}");
                if (issue.Remediation is not null)
                {
                    Console.WriteLine($"         fix: {issue.Remediation.Text}");
                }
            }
        }

        return plan.CanProceed ? 0 : 1;
    }
}
