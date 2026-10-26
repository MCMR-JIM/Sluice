using System.Text.Json;
using Sluice.Store;
using Xunit;

namespace Sluice.Tests;

public sealed class StoreProductResolverTests
{
    private static readonly StoreRequest Request = new("9NKSQGP7F2NH", null, null);

    [Fact]
    public async Task TriesTheNextEndpointWhenOneFailsWithSomethingOtherThanAResolutionError()
    {
        // A captive portal or a proxy answers 200 with HTML, or the retries run out and an
        // HttpRequestException escapes. Neither used to fall through to the second endpoint, so the
        // user got a raw JSON parse message instead of a resolution report.
        var failing = new FakeCatalogClient("displaycatalog", () => throw new JsonException("'<' is an invalid start of a value."));
        var working = new FakeCatalogClient("storeedge", () => Product(withPackages: true));

        var resolver = new StoreProductResolver([failing, working]);
        var product = await resolver.ResolveAsync(Request);

        Assert.True(product.HasPackages);
        Assert.Equal(1, working.Calls);
    }

    [Fact]
    public async Task TriesTheNextEndpointWhenTheFirstOneThrowsAnHttpError()
    {
        var failing = new FakeCatalogClient("displaycatalog", () => throw new HttpRequestException("no route to host"));
        var working = new FakeCatalogClient("storeedge", () => Product(withPackages: true));

        var resolver = new StoreProductResolver([failing, working]);
        var product = await resolver.ResolveAsync(Request);

        Assert.True(product.HasPackages);
        Assert.Equal(1, working.Calls);
    }

    [Fact]
    public async Task DoesNotCacheAnAnswerWithoutPackages()
    {
        // A metadata only answer is what a struggling endpoint returns; caching it would keep saying
        // "this listing has no packages, it is probably a Win32 app" for the rest of the process even
        // after the endpoint recovered.
        var flaky = new FakeCatalogClient("displaycatalog", () => Product(withPackages: false));
        var resolver = new StoreProductResolver([flaky]);

        var first = await resolver.ResolveAsync(Request);
        Assert.False(first.HasPackages);

        flaky.Behaviour = () => Product(withPackages: true);
        var second = await resolver.ResolveAsync(Request);

        Assert.True(second.HasPackages);
        Assert.Equal(2, flaky.Calls);
    }

    [Fact]
    public async Task ReportsEveryEndpointWhenNoneOfThemAnswers()
    {
        var first = new FakeCatalogClient("displaycatalog", () => throw new JsonException("not json"));
        var second = new FakeCatalogClient("storeedge", () => null);

        var resolver = new StoreProductResolver([first, second]);

        var error = await Assert.ThrowsAsync<StoreResolutionException>(() => resolver.ResolveAsync(Request));
        Assert.Contains("displaycatalog", error.Message, StringComparison.Ordinal);
        Assert.Contains("storeedge", error.Message, StringComparison.Ordinal);
    }

    private static StoreProduct Product(bool withPackages) => new(
        "9NKSQGP7F2NH",
        "Contoso Notes",
        "Contoso",
        "Contoso.Notes_abcdefghij",
        withPackages
            ? [new StorePackageGroup("11111111-1111-1111-1111-111111111111", "MsixBundle", "Contoso.Notes_abcdefghij", ["x64"])]
            : [],
        "US",
        "en-US",
        "fake");

    private sealed class FakeCatalogClient(string name, Func<StoreProduct?> behaviour) : IStoreCatalogClient
    {
        public string Name { get; } = name;

        public int Calls { get; private set; }

        public Func<StoreProduct?> Behaviour { get; set; } = behaviour;

        public Task<StoreProduct?> GetProductAsync(string productId, string market, string locale, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Behaviour());
        }
    }
}
