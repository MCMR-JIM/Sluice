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

    private const string FaultResponse = """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body><s:Fault><faultstring>The session has expired.</faultstring></s:Fault></s:Body>
        </s:Envelope>
        """;

    private const string SyncUpdatesResponse = """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <SyncUpdatesResponse xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
              <SyncUpdatesResult />
            </SyncUpdatesResponse>
          </s:Body>
        </s:Envelope>
        """;

    [Fact]
    public async Task RetriesATransientStatusInsteadOfSendingADisposedBody()
    {
        // A busy delivery service answers 500 and then works. The retry must survive that: it used to
        // be suspected of re-sending a disposed body, and this test pins the behaviour either way.
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

    [Fact]
    public async Task RenewsTheSessionCookieWhenTheServiceFaultsOnAnExpiredOne()
    {
        // The session cookie expires while the window is open. The service answers such a request with
        // a fault that says nothing about the cookie, so a client that fetches one cookie per instance
        // keeps failing - reading exactly like an outage - until the process restarts.
        var handler = new SessionHandler();
        using var client = new Fe3Client(new HttpClient(handler));

        await client.GetPackagesAsync("11111111-1111-1111-1111-111111111111");
        Assert.Equal(1, handler.CookieRequests);

        handler.ExpireSession();
        var packages = await client.GetPackagesAsync("11111111-1111-1111-1111-111111111111");

        Assert.NotNull(packages);
        Assert.Equal(2, handler.CookieRequests);
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

    /// <summary>Serves a cookie, then faults on any request that presents an expired one.</summary>
    private sealed class SessionHandler : HttpMessageHandler
    {
        private int _issued;

        public int CookieRequests { get; private set; }

        public string? ValidCookie { get; private set; }

        public void ExpireSession() => ValidCookie = null;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (body.Contains("<GetCookie", StringComparison.Ordinal))
            {
                CookieRequests++;
                ValidCookie = $"cookie-{++_issued}";
                return Xml(HttpStatusCode.OK, CookieResponse.Replace("session-cookie-value", ValidCookie, StringComparison.Ordinal));
            }

            if (ValidCookie is null || !body.Contains(ValidCookie, StringComparison.Ordinal))
            {
                return Xml(HttpStatusCode.BadRequest, FaultResponse);
            }

            return Xml(HttpStatusCode.OK, SyncUpdatesResponse);
        }

        private static HttpResponseMessage Xml(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/soap+xml") };
    }
}
