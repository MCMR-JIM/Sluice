using Sluice.Fe3;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// The envelope is built by string interpolation, so every value placed in it has to be escaped. A locale
/// that arrives from a Store link, a category id or a differently wrapped cookie used to break the request
/// itself: the delivery service answered a fault, and the fault described the service rather than the value
/// that made the request unparsable.
/// </summary>
public sealed class WuSoapTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public void SyncUpdatesSurvivesValuesThatAreNotPlainText()
    {
        var envelope = WuSoap.Envelope(
            "SyncUpdates",
            "https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx",
            Now,
            Now.AddMinutes(10),
            WuSoap.RetailTicket,
            WuSoap.SyncUpdatesBody("cookie&value", "category<id>", "zh-CN&<x>"));

        var document = WuSoap.ParseDocument(envelope);
        var values = document.Descendants().Select(element => element.Value).ToList();

        Assert.Contains("cookie&value", values);
        Assert.Contains("category<id>", values);
    }

    [Fact]
    public void FileUrlRequestsSurviveAValueThatIsNotPlainText()
    {
        var envelope = WuSoap.Envelope(
            "GetExtendedUpdateInfo2",
            "https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx/secured",
            Now,
            Now.AddMinutes(10),
            WuSoap.RetailTicket,
            WuSoap.FileUrlBody([("update&1", 2)], "en-US"));

        var document = WuSoap.ParseDocument(envelope);
        var attributes = document.Descendants()
            .First(element => element.Name.LocalName == "deviceAttributes")
            .Value;

        Assert.Contains("InstallLanguage=en-US", attributes, StringComparison.Ordinal);
    }

    [Fact]
    public void APlainEnvelopeIsUnchanged()
    {
        var body = WuSoap.SyncUpdatesBody("cookie", "11111111-1111-1111-1111-111111111111", "zh-Hans-CN");
        var envelope = WuSoap.Envelope("SyncUpdates", "https://example.invalid/client.asmx", Now, Now.AddMinutes(10), WuSoap.RetailTicket, body);

        var document = WuSoap.ParseDocument(envelope);
        var id = document.Descendants().First(element => element.Name.LocalName == "Id").Value;

        Assert.Equal("11111111-1111-1111-1111-111111111111", id);
        Assert.DoesNotContain("&amp;", document.ToString(), StringComparison.Ordinal);
    }
}
