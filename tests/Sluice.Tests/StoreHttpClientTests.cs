using Sluice.Http;
using Xunit;

namespace Sluice.Tests;

/// <summary>
/// The user agent is the only thing Microsoft's endpoints learn about this tool. It used to say
/// <c>sluice/0.1</c> and point at a repository that does not exist, so a server log could not tell which
/// build made a request or where it came from.
/// </summary>
public sealed class StoreHttpClientTests
{
    [Fact]
    public void TheUserAgentNamesTheRunningVersionAndThisProject()
    {
        Assert.Contains(Sluice.BuildInfo.Version, StoreHttpClient.UserAgent, StringComparison.Ordinal);
        Assert.Contains(Sluice.BuildInfo.RepositoryUrl, StoreHttpClient.UserAgent, StringComparison.Ordinal);
        Assert.DoesNotContain("sluice/0.1", StoreHttpClient.UserAgent, StringComparison.Ordinal);
    }

    [Fact]
    public void TheClientAcceptsTheUserAgentItSends()
    {
        // Exactly what StoreHttpClient does when it builds a client: a user agent that does not parse is
        // a client that throws on the first request instead of resolving a product.
        using var request = new System.Net.Http.HttpRequestMessage();

        request.Headers.UserAgent.ParseAdd(StoreHttpClient.UserAgent);

        Assert.Contains(request.Headers.UserAgent, value => value.Product?.Name == "sluice");
    }
}
