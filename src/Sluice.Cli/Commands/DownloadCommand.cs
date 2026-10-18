using Sluice.Cli.Cli;
using Sluice.Download;
using Sluice.Packages;
using Sluice.Resolution;

namespace Sluice.Cli.Commands;

/// <summary>Resolves a Store reference and downloads the packages it needs.</summary>
internal static class DownloadCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var arguments = CommandLineArguments.Parse(args);
        if (arguments.Positionals.Count == 0)
        {
            Console.Error.WriteLine("error: download needs a Store URL or product id");
            return 2;
        }

        var reference = arguments.Positionals[0];
        var architecture = WindowsArchitecture.Normalize(arguments.Get("arch") ?? WindowsArchitecture.Current);
        var locale = arguments.Get("locale") ?? Defaults.Locale;
        var directory = arguments.Get("dir") ?? Path.Combine(Environment.CurrentDirectory, "downloads");
        var dependenciesOnly = arguments.Has("deps-only") || arguments.Has("dependencies-only");
        var json = arguments.Has("json");

        using var resolver = new StorePackageResolver();
        var resolution = await resolver.ResolveAsync(reference, locale, CancellationToken.None).ConfigureAwait(false);

        var selection = PackageSelector.Select(
            resolution,
            new PackageSelectionOptions(
                architecture,
                locale,
                IncludeAllArchitectures: arguments.Has("all-arch") || arguments.Has("all-architectures")));

        var wanted = new List<ResolvedPackage>();
        if (!dependenciesOnly)
        {
            wanted.AddRange(selection.Main);
        }

        wanted.AddRange(selection.Dependencies);
        wanted.AddRange(selection.Resources);

        var only = arguments.Get("only");
        if (!string.IsNullOrWhiteSpace(only))
        {
            wanted = wanted
                .Where(package => package.Package.ReadableFileName.Contains(only, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (wanted.Count == 0)
        {
            Console.Error.WriteLine("error: nothing matched the selection");
            return 1;
        }

        if (!json)
        {
            Console.WriteLine($"Product : {resolution.Product.Title} ({resolution.Product.ProductId})");
            Console.WriteLine($"Target  : {architecture}, locale {locale}");
            Console.WriteLine($"Folder  : {directory}");
            Console.WriteLine();
            foreach (var package in wanted)
            {
                Console.WriteLine($"  {package.Package.ReadableFileName}  ({TablePrinter.FormatSize(package.Package.SizeInBytes)})");
            }

            Console.WriteLine();
        }

        using var downloader = new PackageDownloader();
        var progress = json ? null : new ProgressRenderer();
        var options = new DownloadOptions(
            MaxParallelism: Math.Clamp(arguments.GetInt32("parallel", 3), 1, 16),
            Overwrite: arguments.Has("overwrite"),
            VerifyDigest: !arguments.Has("no-verify"),
            Progress: progress is null ? null : progress.Report);

        IReadOnlyList<DownloadResult> results;
        try
        {
            results = await downloader.DownloadManyAsync(wanted, directory, options, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            progress?.Finish();
        }

        var manifest = DownloadManifest.FromResults(resolution, results, architecture, locale);
        await DownloadManifest.SaveAsync(directory, manifest, CancellationToken.None).ConfigureAwait(false);

        if (json)
        {
            JsonOutput.Write(new
            {
                product = new { resolution.Product.ProductId, resolution.Product.Title, architecture, locale },
                directory,
                packages = results.Select(result => new
                {
                    file = Path.GetFileName(result.Path),
                    identity = result.Package.InstallerSpecificIdentifier,
                    sizeInBytes = result.SizeInBytes,
                    verification = result.Verification.ToString(),
                    reused = result.Reused,
                }),
            });
            return 0;
        }

        Console.WriteLine();
        TablePrinter.Print(
            ["FILE", "SIZE", "CHECK", "STATE"],
            results
                .Select(result => (IReadOnlyList<string>)
                [
                    Path.GetFileName(result.Path),
                    TablePrinter.FormatSize(result.SizeInBytes),
                    result.Verification.ToString().ToLowerInvariant(),
                    result.Reused ? "reused" : "downloaded",
                ])
                .ToList());

        Console.WriteLine();
        Console.WriteLine($"{results.Count} package(s), {TablePrinter.FormatSize(results.Sum(result => result.SizeInBytes))} in {directory}");
        Console.WriteLine($"Manifest: {Path.Combine(directory, DownloadManifest.FileName)}");
        return 0;
    }

    /// <summary>Renders a single self-updating progress line.</summary>
    private sealed class ProgressRenderer
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, long> _totals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _received = new(StringComparer.Ordinal);
        private readonly bool _interactive = !Console.IsOutputRedirected;

        public void Report(DownloadProgress progress)
        {
            lock (_gate)
            {
                _totals[progress.FileName] = progress.TotalBytes;
                _received[progress.FileName] = progress.BytesReceived;

                if (!_interactive)
                {
                    return;
                }

                var total = _totals.Values.Sum();
                var received = _received.Values.Sum();
                var percent = total > 0 ? received * 100d / total : 0;
                Console.Write($"\r  downloading: {percent,5:0.0}%  {TablePrinter.FormatSize(received)} / {TablePrinter.FormatSize(total)}   ");
            }
        }

        public void Finish()
        {
            if (_interactive)
            {
                Console.Write("\r" + new string(' ', 78) + "\r");
            }
        }
    }
}
