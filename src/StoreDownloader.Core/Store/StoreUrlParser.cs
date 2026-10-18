using System.Text.RegularExpressions;

namespace StoreDownloader.Store;

/// <summary>A normalised Store lookup request derived from user input.</summary>
/// <param name="ProductId">Uppercase Store product id, for example <c>9NKSQGP7F2NH</c>.</param>
/// <param name="Market">Optional market hint taken from the link, for example <c>US</c>.</param>
/// <param name="Locale">Optional locale hint taken from the link, for example <c>en-US</c>.</param>
public sealed record StoreRequest(string ProductId, string? Market, string? Locale);

/// <summary>
/// Turns anything a user is likely to paste (Store links, deep links or bare product ids)
/// into a <see cref="StoreRequest"/>.
/// </summary>
public static partial class StoreUrlParser
{
    /// <summary>Default market used when the input carries no market hint.</summary>
    public const string DefaultMarket = "US";

    /// <summary>Default locale used when the input carries no locale hint.</summary>
    public const string DefaultLocale = "en-US";

    [GeneratedRegex("^[0-9A-Za-z]{12,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex ProductIdPattern();

    [GeneratedRegex("^ms-windows-store://", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeepLinkPattern();

    /// <summary>
    /// Parses <paramref name="input"/> into a <see cref="StoreRequest"/>.
    /// </summary>
    /// <param name="input">Store URL, <c>ms-windows-store://</c> deep link or a bare product id.</param>
    /// <param name="request">The parsed request when the method returns <c>true</c>.</param>
    /// <param name="error">A human readable reason when the method returns <c>false</c>.</param>
    /// <returns><c>true</c> when a product id could be extracted.</returns>
    public static bool TryParse(string? input, out StoreRequest request, out string? error)
    {
        request = null!;
        error = null;

        var text = input?.Trim().Trim('"', '\'', '<', '>');
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "No Store URL or product id was supplied.";
            return false;
        }

        if (IsProductId(text))
        {
            request = new StoreRequest(text.ToUpperInvariant(), null, null);
            return true;
        }

        if (DeepLinkPattern().IsMatch(text))
        {
            var queryStart = text.IndexOf('?', StringComparison.Ordinal);
            var query = queryStart >= 0 ? ParseQuery(text[queryStart..]) : ParseQuery(string.Empty);
            var segments = text[queryStart >= 0 ? ..queryStart : ..].Split('/', StringSplitOptions.RemoveEmptyEntries);

            var productId = FirstProductId(query.Values.SelectMany(v => v)) ?? FirstProductId(segments);
            if (productId is null)
            {
                error = $"The deep link '{text}' does not contain a Store product id.";
                return false;
            }

            request = new StoreRequest(productId, query.Get("market") ?? query.Get("gl"), query.Get("locale") ?? query.Get("hl"));
            return true;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            error = $"'{text}' is neither a product id nor an absolute URL.";
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            error = $"Unsupported URL scheme '{uri.Scheme}'. Use an http(s) Store link, a ms-windows-store deep link or a bare product id.";
            return false;
        }

        var parameters = ParseQuery(uri.Query);
        var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var fromQueryOrPath = FirstProductId(parameters.Values.SelectMany(v => v)) ?? FirstProductId(pathSegments);
        if (fromQueryOrPath is null)
        {
            error = $"The URL '{text}' does not contain a Store product id (expected something like https://apps.microsoft.com/detail/9NKSQGP7F2NH).";
            return false;
        }

        request = new StoreRequest(
            fromQueryOrPath,
            Normalise(parameters.Get("market") ?? parameters.Get("gl")),
            Normalise(parameters.Get("locale") ?? parameters.Get("hl")));

        return true;
    }

    /// <summary>Parses <paramref name="input"/>, throwing when no product id can be extracted.</summary>
    /// <param name="input">Store URL, deep link or bare product id.</param>
    /// <returns>The parsed request.</returns>
    /// <exception cref="FormatException">The input does not contain a Store product id.</exception>
    public static StoreRequest Parse(string? input)
    {
        if (!TryParse(input, out var request, out var error))
        {
            throw new FormatException(error);
        }

        return request;
    }

    /// <summary>Returns <c>true</c> when <paramref name="value"/> has the shape of a Store product id.</summary>
    /// <param name="value">Candidate text.</param>
    public static bool IsProductId(string? value) => value is not null && ProductIdPattern().IsMatch(value);

    private static string? FirstProductId(IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (IsProductId(candidate))
            {
                return candidate.ToUpperInvariant();
            }
        }

        return null;
    }

    private static string? Normalise(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static QueryParameters ParseQuery(string query)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            var key = separator < 0 ? pair : pair[..separator];
            var value = separator < 0 ? string.Empty : pair[(separator + 1)..];
            if (key.Length == 0)
            {
                continue;
            }

            var decodedKey = Uri.UnescapeDataString(key);
            var decodedValue = Uri.UnescapeDataString(value.Replace('+', ' '));
            if (!map.TryGetValue(decodedKey, out var list))
            {
                map[decodedKey] = list = [];
            }

            if (decodedValue.Length > 0)
            {
                list.Add(decodedValue);
            }
        }

        return new QueryParameters(map);
    }

    private sealed class QueryParameters(Dictionary<string, List<string>> map)
    {
        public IEnumerable<IEnumerable<string>> Values => map.Values;

        public string? Get(string key) => map.TryGetValue(key, out var list) && list.Count > 0 ? list[0] : null;
    }
}
