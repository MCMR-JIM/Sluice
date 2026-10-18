using System.Net;
using System.Net.Http.Headers;

namespace StoreDownloader.Http;

/// <summary>Creates the <see cref="HttpClient"/> instances the tool uses everywhere.</summary>
public static class StoreHttpClient
{
    /// <summary>User agent sent to Microsoft endpoints.</summary>
    public const string UserAgent = "storedl/0.1 (+https://github.com/storedl/storedl)";

    /// <summary>Creates a client configured for JSON catalog calls.</summary>
    public static HttpClient CreateCatalogClient() => Create(TimeSpan.FromSeconds(100));

    /// <summary>Creates a client configured for long package downloads.</summary>
    public static HttpClient CreateDownloadClient() => Create(Timeout.InfiniteTimeSpan);

    private static HttpClient Create(TimeSpan timeout)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            ConnectTimeout = TimeSpan.FromSeconds(30),
            AllowAutoRedirect = true,
            MaxConnectionsPerServer = 8,
        };

        var client = new HttpClient(handler) { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}

/// <summary>Retry helpers for the flaky-in-practice Microsoft endpoints.</summary>
public static class HttpRetry
{
    /// <summary>
    /// Sends a request, retrying transient failures with exponential backoff.
    /// </summary>
    /// <param name="client">Client to use.</param>
    /// <param name="requestFactory">Builds a fresh request per attempt (requests are single use).</param>
    /// <param name="attempts">Maximum number of attempts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        int attempts = 3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(requestFactory);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);

        Exception? lastError = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var request = requestFactory();
            try
            {
                var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!IsTransient(response.StatusCode) || attempt == attempts)
                {
                    return response;
                }

                response.Dispose();
                lastError = new HttpRequestException($"Transient status {(int)response.StatusCode} from {request.RequestUri}.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
                if (attempt == attempts)
                {
                    throw;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(400 * Math.Pow(2, attempt - 1)), cancellationToken).ConfigureAwait(false);
        }

        throw new HttpRequestException($"Request failed after {attempts} attempts.", lastError);
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || (int)status >= 500;
}
