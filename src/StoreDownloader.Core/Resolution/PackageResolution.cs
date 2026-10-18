using StoreDownloader.Fe3;
using StoreDownloader.Store;

namespace StoreDownloader.Resolution;

/// <summary>A package discovered for a product, with its (time limited) download URL.</summary>
/// <param name="Package">Package description from the delivery service.</param>
/// <param name="DownloadUrl">Signed CDN URL, or <c>null</c> when the service exposed none.</param>
public sealed record ResolvedPackage(WuPackage Package, string? DownloadUrl)
{
    /// <summary>True when a URL could be resolved.</summary>
    public bool IsDownloadable => !string.IsNullOrWhiteSpace(DownloadUrl);
}

/// <summary>The complete resolution of a Store product.</summary>
/// <param name="Product">Product metadata.</param>
/// <param name="Packages">Every package the product's categories expose.</param>
public sealed record PackageResolution(StoreProduct Product, IReadOnlyList<ResolvedPackage> Packages)
{
    /// <summary>Downloadable packages only.</summary>
    public IReadOnlyList<ResolvedPackage> Downloadable => Packages.Where(package => package.IsDownloadable).ToList();

    /// <summary>Total size of the downloadable packages.</summary>
    public long TotalSizeInBytes => Downloadable.Sum(package => package.Package.SizeInBytes);
}
