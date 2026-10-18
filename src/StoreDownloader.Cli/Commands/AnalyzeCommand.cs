using StoreDownloader.Cli.Cli;
using StoreDownloader.Packages;

namespace StoreDownloader.Cli.Commands;

/// <summary>Shows what is inside package files, without installing anything.</summary>
internal static class AnalyzeCommand
{
    public static Task<int> RunAsync(string[] args)
    {
        var arguments = CommandLineArguments.Parse(args);
        if (arguments.Positionals.Count == 0)
        {
            Console.Error.WriteLine("error: analyze needs a package file or a folder");
            return Task.FromResult(2);
        }

        var path = arguments.Positionals[0];
        var files = PackageFileLocator.Resolve(path);
        if (files.Count == 0)
        {
            Console.Error.WriteLine($"error: no package files found in '{path}'");
            return Task.FromResult(1);
        }

        var analyses = new List<PackageAnalysis>();
        foreach (var file in files)
        {
            var analysis = PackageAnalyzer.TryAnalyze(file);
            if (analysis is null)
            {
                Console.Error.WriteLine($"warning: skipped '{Path.GetFileName(file)}' (not a readable package)");
                continue;
            }

            analyses.Add(analysis);
        }

        if (arguments.Has("json"))
        {
            JsonOutput.Write(analyses.Select(analysis => new
            {
                file = analysis.FileName,
                kind = analysis.Kind.ToString(),
                name = analysis.Identity?.Name,
                version = analysis.Identity?.Version,
                architecture = analysis.Identity?.Architecture,
                resourceId = analysis.Identity?.ResourceId,
                displayName = analysis.DisplayName,
                publisher = analysis.Identity?.Publisher,
                targetDeviceFamily = analysis.TargetDeviceFamily,
                minimumWindowsVersion = analysis.MinimumWindowsVersion,
                dependencies = analysis.Dependencies.Select(dependency => dependency.ToString()),
                children = analysis.Children.Select(child => new
                {
                    file = child.FileName,
                    type = child.Type,
                    architecture = child.Architecture,
                    resourceId = child.ResourceId,
                }),
            }));
            return Task.FromResult(0);
        }

        var architecture = WindowsArchitecture.Normalize(arguments.Get("arch") ?? WindowsArchitecture.Current);
        foreach (var analysis in analyses)
        {
            Console.WriteLine($"{analysis.FileName}");
            Console.WriteLine($"  kind       : {analysis.Kind}");
            if (analysis.Identity is not null)
            {
                Console.WriteLine($"  identity   : {analysis.Identity.Name} {analysis.Identity.Version} ({analysis.Identity.Architecture ?? "neutral"})");
                Console.WriteLine($"  publisher  : {analysis.Identity.Publisher ?? "unknown"}");
            }

            if (analysis.DisplayName is not null)
            {
                Console.WriteLine($"  display    : {analysis.DisplayName}");
            }

            if (analysis.TargetDeviceFamily is not null)
            {
                Console.WriteLine($"  targets    : {analysis.TargetDeviceFamily} >= {analysis.MinimumWindowsVersion}");
            }

            if (analysis.Dependencies.Count > 0)
            {
                Console.WriteLine("  depends on :");
                foreach (var dependency in analysis.Dependencies)
                {
                    Console.WriteLine($"      - {dependency}");
                }
            }

            if (analysis.Children.Count > 0)
            {
                Console.WriteLine($"  contains   : {analysis.Children.Count} child package(s)");
                TablePrinter.Print(
                    ["  CHILD", "TYPE", "ARCH", "RESOURCE"],
                    analysis.Children
                        .Select(child => (IReadOnlyList<string>)
                        [
                            "  " + child.FileName,
                            child.Type ?? "-",
                            child.Architecture ?? "-",
                            child.ResourceId ?? "-",
                        ])
                        .ToList());

                var selection = BundlePackageSelector.Select(analysis, architecture);
                if (selection.Application is not null)
                {
                    Console.WriteLine($"  for {architecture} : {selection.Application.FileName}");
                }
            }

            Console.WriteLine();
        }

        return Task.FromResult(0);
    }
}
