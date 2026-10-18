namespace StoreDownloader.Store;

/// <summary>
/// One Windows Update category behind a Store product. A category is the handle the
/// delivery service (FE3) understands and it is what actually resolves to package files.
/// </summary>
/// <param name="WuCategoryId">Category id passed to <c>SyncUpdates</c>.</param>
/// <param name="PackageFormat">Format advertised by the catalog, for example <c>MsixBundle</c>.</param>
/// <param name="PackageFamilyName">Package family name advertised by the catalog.</param>
/// <param name="Architectures">Architectures advertised by the catalog.</param>
public sealed record StorePackageGroup(
    string WuCategoryId,
    string? PackageFormat,
    string? PackageFamilyName,
    IReadOnlyList<string> Architectures);

/// <summary>Product metadata resolved from a Microsoft Store catalog endpoint.</summary>
/// <param name="ProductId">Store product id.</param>
/// <param name="Title">Display title.</param>
/// <param name="Publisher">Publisher display name.</param>
/// <param name="PackageFamilyName">Primary package family name, when known.</param>
/// <param name="PackageGroups">Windows Update categories that hold the actual packages.</param>
/// <param name="Market">Market the metadata was resolved for.</param>
/// <param name="Locale">Locale the metadata was resolved for.</param>
/// <param name="Source">Catalog endpoint that answered, for diagnostics.</param>
public sealed record StoreProduct(
    string ProductId,
    string Title,
    string? Publisher,
    string? PackageFamilyName,
    IReadOnlyList<StorePackageGroup> PackageGroups,
    string Market,
    string Locale,
    string Source)
{
    /// <summary>True when at least one Windows Update category was advertised.</summary>
    public bool HasPackages => PackageGroups.Count > 0;

    /// <summary>Distinct Windows Update category ids in discovery order.</summary>
    public IReadOnlyList<string> WuCategoryIds =>
        PackageGroups.Select(group => group.WuCategoryId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
