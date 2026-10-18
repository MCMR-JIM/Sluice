using Sluice.Cli.Cli;
using Sluice.Diagnostics;

namespace Sluice.Cli.Commands;

/// <summary>Explains a deployment error code and how to get past it.</summary>
internal static class ExplainCommand
{
    public static int RunAsync(string[] args)
    {
        var arguments = CommandLineArguments.Parse(args);

        if (arguments.Positionals.Count == 0 || arguments.Has("list"))
        {
            TablePrinter.Print(
                ["CODE", "NAME", "MEANING"],
                AppxErrorCatalog.All
                    .Select(entry => (IReadOnlyList<string>)[entry.FormattedCode, entry.Name, entry.Meaning])
                    .ToList());
            return 0;
        }

        var input = arguments.Positionals[0];
        if (!AppxErrorCatalog.TryParse(input, out var code))
        {
            Console.Error.WriteLine($"error: '{input}' is not an HRESULT (try 0x80073CF3 or 2147950323)");
            return 2;
        }

        if (arguments.Has("json"))
        {
            if (!AppxErrorCatalog.TryLookup(code, out var info))
            {
                JsonOutput.Write(new { code = AppxErrorCatalog.Format(code), known = false });
                return 1;
            }

            JsonOutput.Write(new
            {
                code = info.FormattedCode,
                known = true,
                info.Name,
                info.Meaning,
                info.TypicalCause,
                remedy = info.Remedy.ToString(),
                manualSteps = info.ManualSteps,
            });
            return 0;
        }

        if (!AppxErrorCatalog.TryLookup(code, out var entry))
        {
            Console.WriteLine($"{AppxErrorCatalog.Format(code)} is not in the catalog.");
            Console.WriteLine("Run 'sluice explain --list' to see the codes that are documented.");
            return 1;
        }

        Console.WriteLine($"{entry.FormattedCode} {entry.Name}");
        Console.WriteLine($"  what : {entry.Meaning}");
        Console.WriteLine($"  why  : {entry.TypicalCause}");
        Console.WriteLine($"  auto : {Describe(entry.Remedy)}");
        Console.WriteLine($"  fix  : {entry.ManualSteps}");
        return 0;
    }

    private static string Describe(DeploymentRemedy remedy) => remedy switch
    {
        DeploymentRemedy.None => "nothing automatic; the machine or the file has to change",
        DeploymentRemedy.ForceApplicationShutdown => "retries with -ForceApplicationShutdown",
        DeploymentRemedy.ForceUpdateFromAnyVersion => "retries with -ForceUpdateFromAnyVersion",
        DeploymentRemedy.SupplyDependencies => "retries with -DependencyPath for the planned frameworks",
        DeploymentRemedy.RegisterLooseFiles => "extracts the package and re-registers AppxManifest.xml (needs Developer Mode)",
        DeploymentRemedy.EnableSideloading => "reports the policy setting that has to change first",
        DeploymentRemedy.RemoveConflictingPackage => "reports the conflicting package to remove",
        _ => remedy.ToString(),
    };
}
