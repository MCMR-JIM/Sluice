using Sluice.Store;
using Xunit;

namespace Sluice.Tests;

public sealed class StoreUrlParserTests
{
    [Theory]
    [InlineData("9NKSQGP7F2NH")]
    [InlineData("9nksqgp7f2nh")]
    [InlineData("  9NKSQGP7F2NH  ")]
    [InlineData("XP8BT8DW290MPQ")]
    public void ParsesBareProductIds(string input)
    {
        Assert.True(StoreUrlParser.TryParse(input, out var request, out var error));
        Assert.Null(error);
        Assert.Equal(input.Trim().ToUpperInvariant(), request.ProductId);
        Assert.Null(request.Market);
        Assert.Null(request.Locale);
    }

    [Theory]
    [InlineData("https://apps.microsoft.com/detail/9nksqgp7f2nh")]
    [InlineData("https://apps.microsoft.com/store/detail/whatsapp/9NKSQGP7F2NH")]
    [InlineData("https://apps.microsoft.com/detail/9NKSQGP7F2NH?hl=en-us&gl=US")]
    [InlineData("https://www.microsoft.com/store/apps/9NKSQGP7F2NH")]
    [InlineData("https://www.microsoft.com/en-us/p/whatsapp/9nksqgp7f2nh")]
    [InlineData("https://apps.microsoft.com/detail/9NKSQGP7F2NH?cid=msft_web_collection")]
    public void ParsesStoreUrls(string input)
    {
        Assert.True(StoreUrlParser.TryParse(input, out var request, out var error));
        Assert.Null(error);
        Assert.Equal("9NKSQGP7F2NH", request.ProductId);
    }

    [Fact]
    public void TrimsHintsFromADeepLinkLikeItTrimsThemFromAWebLink()
    {
        // A pasted deep link can carry escaped spaces. The web form trimmed the hints; the deep link form
        // did not, so " CN " travelled into the catalog request, which answered 400 for a market that
        // looks perfectly fine to the person who pasted it.
        Assert.True(StoreUrlParser.TryParse(
            "ms-windows-store://pdp/?ProductId=9NKSQGP7F2NH&market=%20CN%20&hl=%20zh-CN%20",
            out var request,
            out var error));

        Assert.Null(error);
        Assert.Equal("9NKSQGP7F2NH", request.ProductId);
        Assert.Equal("CN", request.Market);
        Assert.Equal("zh-CN", request.Locale);
    }

    [Fact]
    public void ReadsLocaleAndMarketHintsFromQuery()
    {
        Assert.True(StoreUrlParser.TryParse(
            "https://apps.microsoft.com/detail/9NKSQGP7F2NH?hl=zh-cn&gl=CN",
            out var request,
            out _));

        Assert.Equal("zh-cn", request.Locale);
        Assert.Equal("CN", request.Market);
    }

    [Theory]
    [InlineData("ms-windows-store://pdp/?ProductId=9NKSQGP7F2NH")]
    [InlineData("ms-windows-store://pdp/?productid=9nksqgp7f2nh&mode=mini")]
    [InlineData("MS-WINDOWS-STORE://PDP/?ProductId=9NKSQGP7F2NH")]
    public void ParsesDeepLinks(string input)
    {
        Assert.True(StoreUrlParser.TryParse(input, out var request, out var error));
        Assert.Null(error);
        Assert.Equal("9NKSQGP7F2NH", request.ProductId);
    }

    [Theory]
    [InlineData("https://apps.microsoft.com/detail/9NKSQGP7F2NH?ocid=9ABCDEFGHIJK")]
    [InlineData("https://apps.microsoft.com/detail/9NKSQGP7F2NH?referrer=9ABCDEFGHIJK&hl=en-us")]
    [InlineData("ms-windows-store://pdp/?ocid=9ABCDEFGHIJK&ProductId=9NKSQGP7F2NH")]
    public void PrefersTheRealProductIdOverATrackingValueOfTheSameShape(string input)
    {
        // Tracking parameters are opaque strings; one that happens to look like a product id must not
        // win, because resolving the wrong product is worse than refusing the link.
        Assert.True(StoreUrlParser.TryParse(input, out var request, out var error));
        Assert.Null(error);
        Assert.Equal("9NKSQGP7F2NH", request.ProductId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("whatsapp")]
    [InlineData("https://apps.microsoft.com/detail/whatsapp")]
    [InlineData("ftp://apps.microsoft.com/detail/9NKSQGP7F2NH")]
    public void RejectsInputWithoutProductId(string? input)
    {
        Assert.False(StoreUrlParser.TryParse(input, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void ParseThrowsForInvalidInput()
    {
        Assert.Throws<FormatException>(() => StoreUrlParser.Parse("nonsense"));
    }
}
