using System.Collections.Concurrent;
using Sluice.Http;

namespace Sluice.Store;

/// <summary>
/// Resolves a <see cref="StoreRequest"/> into a <see cref="StoreProduct"/>, trying several
/// catalog endpoints and keeping the first answer that actually carries package categories.
/// </summary>
public sealed class StoreProductResolver
{
    private readonly IReadOnlyList<IStoreCatalogClient> _clients;
    // Resolutions can run concurrently (the window probes and the command line do), and only answers
    // that actually carry packages are worth remembering.
    private readonly ConcurrentDictionary<string, StoreProduct> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a resolver backed by the default catalog endpoints.</summary>
    /// <param name="httpClient">Optional client; a catalog client is created when omitted.</param>
    public StoreProductResolver(HttpClient? httpClient = null)
        : this(httpClient ?? StoreHttpClient.CreateCatalogClient(), disposeClient: httpClient is null)
    {
    }

    /// <summary>Creates a resolver over explicit catalog clients.</summary>
    /// <param name="clients">Catalog clients in priority order.</param>
    public StoreProductResolver(IReadOnlyList<IStoreCatalogClient> clients)
    {
        _clients = clients;
        OwnedClient = null;
    }

    private StoreProductResolver(HttpClient httpClient, bool disposeClient)
    {
        _clients =
        [
            new DisplayCatalogClient(httpClient),
            new StoreEdgeClient(httpClient),
        ];
        OwnedClient = disposeClient ? httpClient : null;
    }

    /// <summary>Client owned by this resolver, when it created one.</summary>
    public HttpClient? OwnedClient { get; }

    /// <summary>Catalog endpoints this resolver will try, in order.</summary>
    public IEnumerable<string> Endpoints => _clients.Select(client => client.Name);

    /// <summary>
    /// Resolves product metadata for <paramref name="request"/>.
    /// </summary>
    /// <param name="request">Request produced by <see cref="StoreUrlParser"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Product metadata carrying at least one Windows Update category.</returns>
    /// <exception cref="StoreResolutionException">No endpoint produced a usable answer.</exception>
    public async Task<StoreProduct> ResolveAsync(StoreRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var market = string.IsNullOrWhiteSpace(request.Market) ? StoreUrlParser.DefaultMarket : request.Market.ToUpperInvariant();
        var locale = string.IsNullOrWhiteSpace(request.Locale) ? StoreUrlParser.DefaultLocale : request.Locale;
        var cacheKey = $"{request.ProductId}|{market}|{locale}";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var errors = new List<string>();
        StoreProduct? best = null;
        foreach (var client in _clients)
        {
            try
            {
                var product = await client.GetProductAsync(request.ProductId, market, locale, cancellationToken).ConfigureAwait(false);
                if (product is null)
                {
                    errors.Add($"{client.Name}: unknown product id");
                    continue;
                }

                if (product.HasPackages)
                {
                    _cache[cacheKey] = product;
                    return product;
                }

                errors.Add($"{client.Name}: product has no package categories");
                best ??= product;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Whatever an endpoint threw - a fault, an HTML body where JSON was expected, a socket
                // that gave up after its retries - is a reason to try the next endpoint. Letting it
                // escape meant the fallback was never consulted and the user saw a raw parser message.
                errors.Add($"{client.Name}: {ex.Message}");
            }
        }

        if (best is not null)
        {
            // Deliberately not cached: an answer without package categories is what a struggling
            // endpoint returns, and replaying it for the rest of the process would keep telling the
            // user the listing is a Win32 app long after the endpoint recovered.
            return best;
        }

        throw new StoreResolutionException(
            $"Could not resolve product '{request.ProductId}' (market {market}, locale {locale}). " + string.Join("; ", errors));
    }

    /// <summary>Convenience overload that parses user input first.</summary>
    /// <param name="input">Store URL, deep link or product id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<StoreProduct> ResolveAsync(string input, CancellationToken cancellationToken = default) =>
        ResolveAsync(StoreUrlParser.Parse(input), cancellationToken);
}
