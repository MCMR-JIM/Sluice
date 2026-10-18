namespace StoreDownloader.Store;

/// <summary>A Microsoft Store catalog endpoint that can describe a product.</summary>
public interface IStoreCatalogClient
{
    /// <summary>Short endpoint name used in diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// Resolves product metadata, including the Windows Update categories that hold the packages.
    /// </summary>
    /// <param name="productId">Store product id.</param>
    /// <param name="market">Market such as <c>US</c>.</param>
    /// <param name="locale">Locale such as <c>en-US</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The product, or <c>null</c> when the endpoint does not know the id.</returns>
    Task<StoreProduct?> GetProductAsync(string productId, string market, string locale, CancellationToken cancellationToken = default);
}
