using Sluice.Fe3;
using Sluice.Store;

namespace Sluice.Resolution;

/// <summary>
/// Turns a Store link into downloadable packages: catalog lookup, then one delivery service
/// expansion per Windows Update category, then URL resolution for everything that has one.
/// </summary>
public sealed class StorePackageResolver : IDisposable
{
    private readonly StoreProductResolver _productResolver;
    private readonly Fe3Client _fe3;
    private readonly bool _ownsFe3;

    /// <summary>Creates a resolver.</summary>
    /// <param name="productResolver">Catalog resolver; a default one is created when omitted.</param>
    /// <param name="fe3Client">Delivery service client; a default one is created when omitted.</param>
    public StorePackageResolver(StoreProductResolver? productResolver = null, Fe3Client? fe3Client = null)
    {
        _productResolver = productResolver ?? new StoreProductResolver();
        _ownsFe3 = fe3Client is null;
        _fe3 = fe3Client ?? new Fe3Client();
    }

    /// <summary>Resolves packages for user input (Store URL, deep link or product id).</summary>
    /// <param name="input">User supplied Store reference.</param>
    /// <param name="localeOverride">Locale to use instead of the one hinted by the link.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PackageResolution> ResolveAsync(string input, string? localeOverride = null, CancellationToken cancellationToken = default)
    {
        var request = StoreUrlParser.Parse(input);
        return ResolveAsync(request, localeOverride, cancellationToken);
    }

    /// <summary>Resolves packages for a parsed request.</summary>
    /// <param name="request">Parsed Store request.</param>
    /// <param name="localeOverride">Locale to use instead of the one hinted by the link.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<PackageResolution> ResolveAsync(StoreRequest request, string? localeOverride = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = await _productResolver.ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        var locale = localeOverride
            ?? (string.IsNullOrWhiteSpace(request.Locale) ? product.Locale : request.Locale);

        if (!product.HasPackages)
        {
            throw new StoreResolutionException(
                $"Product '{product.ProductId}' ({product.Title}) advertises no Windows Update category. "
                + "It is probably an EXE/Win32 listing rather than an MSIX/APPX package.");
        }

        var seen = new Dictionary<string, WuPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var categoryId in product.WuCategoryIds)
        {
            var listing = await _fe3.GetPackagesAsync(categoryId, locale, cancellationToken).ConfigureAwait(false);
            foreach (var package in listing.Packages)
            {
                if (!seen.TryAdd(package.InstallerSpecificIdentifier, package))
                {
                    continue;
                }
            }
        }

        var packages = seen.Values.ToList();
        var urls = await _fe3.GetFileUrlsAsync(packages, locale, cancellationToken).ConfigureAwait(false);

        var resolved = packages
            .Select(package => new ResolvedPackage(
                package,
                package.Sha1Digest is not null && urls.TryGetValue(package.Sha1Digest, out var url) ? url : null))
            .OrderByDescending(package => package.Package.SizeInBytes)
            .ToList();

        return new PackageResolution(product, resolved);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsFe3)
        {
            _fe3.Dispose();
        }
    }
}
