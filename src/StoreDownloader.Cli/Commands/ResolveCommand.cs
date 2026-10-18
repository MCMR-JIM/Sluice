using StoreDownloader.Cli.Cli;
using StoreDownloader.Resolution;

namespace StoreDownloader.Cli.Commands;

/// <summary>Resolves a Store reference into the packages behind it.</summary>
internal static class ResolveCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var arguments = CommandLineArguments.Parse(args);
        if (arguments.Positionals.Count == 0)
        {
            Console.Error.WriteLine("error: resolve needs a Store URL or product id");
            return 2;
        }

        var reference = arguments.Positionals[0];
        using var resolver = new StorePackageResolver();
        var resolution = await resolver.ResolveAsync(reference, arguments.Get("locale") ?? Defaults.Locale, CancellationToken.None)
            .ConfigureAwait(false);

        if (arguments.Has("json"))
        {
            WriteJson(resolution);
            return 0;
        }

        WriteHuman(resolution);
        return 0;
    }

    private static void WriteHuman(PackageResolution resolution)
    {
        var product = resolution.Product;
        Console.WriteLine($"Product : {product.Title} ({product.ProductId})");
        Console.WriteLine($"Publisher: {product.Publisher ?? "unknown"}");
        Console.WriteLine($"Catalog : {product.Source}, market {product.Market}, locale {product.Locale}");
        Console.WriteLine($"Family  : {product.PackageFamilyName ?? "unknown"}");
        Console.WriteLine($"Category: {string.Join(", ", product.WuCategoryIds)}");
        Console.WriteLine();

        var rows = resolution.Packages
            .Select(package => (IReadOnlyList<string>)
            [
                package.Package.ReadableFileName,
                package.Package.Architecture ?? "neutral",
                TablePrinter.FormatSize(package.Package.SizeInBytes),
                package.IsDownloadable ? "url" : "no url",
            ])
            .ToList();

        TablePrinter.Print(["FILE", "ARCH", "SIZE", "LINK"], rows);
        Console.WriteLine();
        Console.WriteLine($"{resolution.Packages.Count} package(s), {resolution.Downloadable.Count} downloadable, "
            + $"{TablePrinter.FormatSize(resolution.TotalSizeInBytes)} total");
    }

    private static void WriteJson(PackageResolution resolution)
    {
        var payload = new
        {
            product = new
            {
                resolution.Product.ProductId,
                resolution.Product.Title,
                resolution.Product.Publisher,
                resolution.Product.PackageFamilyName,
                resolution.Product.Market,
                resolution.Product.Locale,
                resolution.Product.Source,
                wuCategoryIds = resolution.Product.WuCategoryIds,
            },
            packages = resolution.Packages.Select(package => new
            {
                file = package.Package.ReadableFileName,
                identity = package.Package.InstallerSpecificIdentifier,
                package.Package.Architecture,
                package.Package.Version,
                sizeInBytes = package.Package.SizeInBytes,
                sha1 = package.Package.Sha1Digest,
                sha256 = package.Package.Sha256Digest,
                url = package.DownloadUrl,
            }),
        };

        JsonOutput.Write(payload);
    }
}
