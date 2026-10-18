using System.Net;
using System.Text.Json;
using Sluice.Http;
using Sluice.Json;

namespace Sluice.Store;

/// <summary>
/// Client for the display catalog (<c>displaycatalog.mp.microsoft.com</c>), the endpoint the
/// Store itself uses to describe products. Note that it rejects requests without a
/// <c>languages</c> parameter.
/// </summary>
public sealed class DisplayCatalogClient(HttpClient httpClient) : IStoreCatalogClient
{
    /// <summary>Base address of the public display catalog.</summary>
    public const string BaseAddress = "https://displaycatalog.mp.microsoft.com/v7.0/products/";

    /// <inheritdoc />
    public string Name => "displaycatalog";

    /// <inheritdoc />
    public async Task<StoreProduct?> GetProductAsync(string productId, string market, string locale, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        var url = BaseAddress + Uri.EscapeDataString(productId)
            + "?languages=" + Uri.EscapeDataString(locale)
            + "&market=" + Uri.EscapeDataString(market)
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
            throw new StoreResolutionException($"display catalog returned {(int)response.StatusCode} for product '{productId}'.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var product = document.RootElement.GetElement("Product");
        if (product is null)
        {
            return null;
        }

        return Parse(productId, market, locale, product.GetValueOrDefault());
    }

    private static StoreProduct Parse(string productId, string market, string locale, JsonElement product)
    {
        var title = PickLocalised(product, locale, "ProductTitle", "ShortTitle", "FriendlyTitle") ?? productId;
        var publisher = PickLocalised(product, locale, "PublisherName", "DeveloperName");

        var groups = new List<StorePackageGroup>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? packageFamilyName = null;

        foreach (var availability in product.GetArray("DisplaySkuAvailabilities"))
        {
            foreach (var package in availability.GetArray("Sku", "Properties", "Packages"))
            {
                var fulfillment = package.GetElement("FulfillmentData");
                if (fulfillment is null)
                {
                    continue;
                }

                var data = fulfillment.GetValueOrDefault();
                packageFamilyName ??= data.GetString("PackageFamilyName");
                var categoryId = data.GetString("WuCategoryId");
                if (string.IsNullOrWhiteSpace(categoryId) || !seen.Add(categoryId))
                {
                    continue;
                }

                groups.Add(new StorePackageGroup(
                    categoryId,
                    package.GetString("PackageFormat"),
                    data.GetString("PackageFamilyName"),
                    NormaliseArchitectures(package.GetStringArray("Architectures"))));
            }
        }

        return new StoreProduct(
            productId.ToUpperInvariant(),
            title,
            publisher,
            packageFamilyName,
            groups,
            market,
            locale,
            "displaycatalog");
    }

    private static string? PickLocalised(JsonElement product, string locale, params string[] propertyNames)
    {
        var localised = product.GetArray("LocalizedProperties");
        var matchesLocale = localised.FirstOrDefault(item =>
            string.Equals(item.GetString("Language"), locale, StringComparison.OrdinalIgnoreCase));
        var chosen = matchesLocale.ValueKind == JsonValueKind.Object
            ? matchesLocale
            : localised.Count > 0 ? localised[0] : default;

        foreach (var name in propertyNames)
        {
            var value = chosen.GetString(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Normalises catalog architecture names to the lowercase forms Windows uses in manifests.</summary>
    /// <param name="architectures">Architecture names such as <c>x64</c> or <c>Arm64</c>.</param>
    public static IReadOnlyList<string> NormaliseArchitectures(IEnumerable<string> architectures) =>
        architectures
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
