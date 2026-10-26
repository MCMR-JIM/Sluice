using Sluice.Cli.Cli;
using Sluice.Diagnostics;
using Sluice.Windows;

namespace Sluice.Cli.Commands;

/// <summary>Reports whether this machine can install sideloaded Store packages at all.</summary>
internal static class DoctorCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var arguments = CommandLineArguments.Parse(args);
        var shell = new PowerShellHost();
        var report = await WindowsEnvironmentProbe.ProbeAsync(shell, CancellationToken.None).ConfigureAwait(false);

        if (arguments.Has("json"))
        {
            // The exit code has to describe the machine, not the output format: a scripted caller used
            // to see 0 for a machine that cannot deploy packages while the text output said 3.
            JsonOutput.Write(report);
            return report.Warnings.Count == 0 ? 0 : 3;
        }

        Console.WriteLine("Environment");
        Console.WriteLine($"  platform      : {(report.IsWindows ? "Windows" : "not Windows")}");
        Console.WriteLine($"  architecture  : {report.Architecture}");
        if (report.IsWindows)
        {
            Console.WriteLine($"  windows       : {report.ProductName ?? "unknown"} {report.DisplayVersion} (build {report.Build})");
            Console.WriteLine($"  sideloading   : {Format(report.SideloadingEnabled)}");
            Console.WriteLine($"  developer mode: {Format(report.DeveloperModeEnabled)}");
            Console.WriteLine($"  elevated      : {Format(report.IsElevated)}");
            Console.WriteLine($"  free space    : {TablePrinter.FormatSize(report.FreeDiskBytes)}");
        }

        Console.WriteLine($"  powershell    : {report.PowerShellPath ?? "not found"}");
        if (report.Services.Count > 0)
        {
            Console.WriteLine("  services      :");
            foreach (var (name, status) in report.Services)
            {
                Console.WriteLine($"      {name,-16} {status}");
            }
        }

        if (report.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Warnings");
            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"  ! {warning}");
            }
        }

        return report.Warnings.Count == 0 ? 0 : 3;
    }

    private static string Format(bool? value) => value switch
    {
        true => "yes",
        false => "no",
        null => "unknown",
    };
}
