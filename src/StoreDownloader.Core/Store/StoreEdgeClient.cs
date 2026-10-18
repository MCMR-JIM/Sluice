using System.Net;
using System.Text.Json;
using StoreDownloader.Http;
using StoreDownloader.Json;

namespace StoreDownloader.Store;

/// <summary>
/// Client for the Store Edge endpoint (<c>storeedgefd.dsx.mp.microsoft.com</c>). This is the
/// endpoint the modern Store web front end talks to; it is the fallback when the display
/// catalog does not answer.
/// </summary>
public sealed class StoreEdgeClient(HttpClient httpClient) : IStoreCatalogClient
{
    /// <summary>Base address of the Store Edge product API.</summary>
    public const string BaseAddress = "https://storeedgefd.dsx.mp.microsoft.com/v9.0/products/";

    /// <inheritdoc />
    public string Name => "storeedge";

    /// <inheritdoc />
    public async Task<StoreProduct?> GetProductAsync(string productId, string market, string locale, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        var url = BaseAddress + Uri.EscapeDataString(productId)
            + "?market=" + Uri.EscapeDataString(market)
            + "&locale=" + Uri.EscapeDataString(locale)
            + "&deviceFamily=Windows.Desktop";

        using var response = await HttpRetry.SendAsync(httpClient, () => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new StoreResolutionException($"Store Edge returned {(int)response.StatusCode} for product '{productId}'.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var payloadElement = document.RootElement.GetElement("Payload");
        if (payloadElement is null)
        {
            return null;
        }

        var payload = payloadElement.GetValueOrDefault();
        var title = payload.GetString("Title") ?? payload.GetString("ShortTitle") ?? productId;
        var families = payload.GetStringArray("PackageFamilyNames");
        var platforms = DisplayCatalogClient.NormaliseArchitectures(payload.GetStringArray("Platforms"));

        var groups = new List<StorePackageGroup>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sku in payload.GetArray("Skus"))
        {
            // Store Edge ships fulfilment data as an embedded JSON *string*, not an object.
            var raw = sku.GetString("FulfillmentData");
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            JsonDocument fulfillment;
            try
            {
                fulfillment = JsonDocument.Parse(raw);
            }
            catch (JsonException)
            {
                continue;
            }

            using (fulfillment)
            {
                var root = fulfillment.RootElement;
                var categoryId = root.GetString("WuCategoryId");
                if (string.IsNullOrWhiteSpace(categoryId) || !seen.Add(categoryId))
                {
                    continue;
                }

                groups.Add(new StorePackageGroup(
                    categoryId,
                    null,
                    root.GetString("PackageFamilyName"),
                    platforms));
            }
        }

        return new StoreProduct(
            productId.ToUpperInvariant(),
            title,
            payload.GetString("PublisherName"),
            families.Count > 0 ? families[0] : groups.FirstOrDefault()?.PackageFamilyName,
            groups,
            market,
            locale,
            "storeedge");
    }
}
