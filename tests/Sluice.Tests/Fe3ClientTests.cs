using System.Net;
using System.Text;
using Sluice.Fe3;
using Xunit;

namespace Sluice.Tests;

public sealed class Fe3ClientTests
{
    private const string CookieResponse = """
        <?xml version="1.0" encoding="utf-8"?>
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <GetCookieResponse xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
              <GetCookieResult>
                <EncryptedData>session-cookie-value</EncryptedData>
              </GetCookieResult>
            </GetCookieResponse>
          </s:Body>
        </s:Envelope>
        """;

    [Fact]
    public async Task RetriesATransientStatusInsteadOfSendingADisposedBody()
    {
        // A busy delivery service answers 500 and then works. The retry used to hand the same
        // StringContent to every attempt while the previous HttpRequestMessage had already been
        // disposed with it, so the second attempt died instead of retrying - which quietly removed
        // the retry path from exactly the endpoint that needs it.
        var handler = new ScriptedHandler(HttpStatusCode.InternalServerError, HttpStatusCode.OK);
        using var client = new Fe3Client(new HttpClient(handler));

        var cookie = await client.GetCookieAsync();

        Assert.Equal("session-cookie-value", cookie);
        Assert.Equal(2, handler.Attempts);
    }

    [Fact]
    public async Task ReportsAFailureInsteadOfRetryingForever()
    {
        var handler = new ScriptedHandler(HttpStatusCode.ServiceUnavailable);
        using var client = new Fe3Client(new HttpClient(handler));

        var error = await Assert.ThrowsAsync<Fe3Exception>(() => client.GetCookieAsync());

        Assert.Contains("503", error.Message, StringComparison.Ordinal);
        Assert.Equal(3, handler.Attempts);
    }

    /// <summary>Answers with the listed statuses in order, repeating the last one.</summary>
    private sealed class ScriptedHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            var status = statuses[Math.Min(Attempts - 1, statuses.Length - 1)];
            var body = status == HttpStatusCode.OK ? CookieResponse : "the service is busy";
            var content = new StringContent(body, Encoding.UTF8, "application/soap+xml");
            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }
    }
}
