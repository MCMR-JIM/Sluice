using Sluice.Cli.Cli;
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
            Console.Error.WriteLine("error: plan needs a folder of downloaded packages");
            return 2;
        }

        var files = PackageFileLocator.Resolve(arguments.Positionals[0]);
        if (files.Count == 0)
        {
            Console.Error.WriteLine($"error: no package files found in '{arguments.Positionals[0]}'");
            return 1;
        }

        var shell = new PowerShellHost();
        var planner = OperatingSystem.IsWindows() && shell.IsAvailable
            ? new InstallPlanner(new AppxInventory(shell))
            : new InstallPlanner();

        var plan = await planner.CreatePlanAsync(
            files,
            new InstallPlannerOptions(
                arguments.Get("arch") ?? WindowsArchitecture.Current,
                arguments.Get("locale")),
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
                    step.Reason,
                ])
                .ToList());

        if (plan.Issues.Count > 0)
        {
            Console.WriteLine();
            foreach (var issue in plan.Issues)
            {
                Console.WriteLine($"[{issue.Severity.ToString().ToLowerInvariant()}] {issue.Message}");
                if (issue.Remediation is not null)
                {
                    Console.WriteLine($"         fix: {issue.Remediation}");
                }
            }
        }

        return plan.CanProceed ? 0 : 1;
    }
}
