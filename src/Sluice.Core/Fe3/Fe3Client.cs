using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Sluice.Http;

namespace Sluice.Fe3;

/// <summary>Raised when the delivery service answers with an error or an unusable payload.</summary>
public sealed class Fe3Exception : SluiceException
{
    /// <summary>Creates an exception with a message.</summary>
    /// <param name="message">Human readable description.</param>
    /// <param name="innerException">Underlying failure.</param>
    public Fe3Exception(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}

/// <summary>The package listing of one Windows Update category.</summary>
/// <param name="WuCategoryId">Category that was expanded.</param>
/// <param name="Packages">Packages found in the category.</param>
/// <param name="ServedLocales">Locales the delivery service answered with.</param>
public sealed record Fe3PackageList(string WuCategoryId, IReadOnlyList<WuPackage> Packages, IReadOnlyList<string> ServedLocales);

/// <summary>
/// Talks to the Windows Update delivery service (FE3): <c>GetCookie</c> for a session cookie,
/// <c>SyncUpdates</c> to expand a Store category into package descriptions, and
/// <c>GetExtendedUpdateInfo2</c> to exchange an update identity for a signed CDN URL.
/// </summary>
public sealed class Fe3Client : IDisposable
{
    /// <summary>Unsecured client web service endpoint.</summary>
    public const string ClientWebServiceUrl = "https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx";

    /// <summary>Secured client web service endpoint used to resolve file URLs.</summary>
    public const string SecuredClientWebServiceUrl = ClientWebServiceUrl + "/secured";

    private const int FileUrlBatchSize = 10;

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private string? _cookie;

    /// <summary>Creates a client, optionally over a caller owned <see cref="HttpClient"/>.</summary>
    /// <param name="httpClient">Client to use; a private one is created when omitted.</param>
    public Fe3Client(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? StoreHttpClient.CreateCatalogClient();
        _ownsClient = httpClient is null;
    }

    /// <summary>Fetches a fresh session cookie from the delivery service.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string> GetCookieAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var soap = WuSoap.Envelope(
            "GetCookie",
            ClientWebServiceUrl,
            now,
            now.AddMinutes(30),
            WuSoap.UserTicket,
            WuSoap.GetCookieBody(now));

        var xml = await PostAsync(ClientWebServiceUrl, soap, cancellationToken).ConfigureAwait(false);
        var document = WuSoap.ParseDocument(xml);
        var cookie = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "EncryptedData")?.Value;

        if (string.IsNullOrWhiteSpace(cookie))
        {
            throw new Fe3Exception("The delivery service did not return a session cookie.");
        }

        _cookie = cookie;
        return cookie;
    }

    /// <summary>Expands a Windows Update category into the packages it contains.</summary>
    /// <param name="wuCategoryId">Category id from the Store catalog.</param>
    /// <param name="locale">Locale reported as client preferred language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Fe3PackageList> GetPackagesAsync(string wuCategoryId, string locale = "en-US", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wuCategoryId);

        var xml = await PostWithSessionAsync(
            cookie =>
            {
                var now = DateTimeOffset.UtcNow;
                return WuSoap.Envelope(
                    "SyncUpdates",
                    ClientWebServiceUrl,
                    now,
                    now.AddMinutes(10),
                    WuSoap.RetailTicket,
                    WuSoap.SyncUpdatesBody(cookie, wuCategoryId, locale));
            },
            ClientWebServiceUrl,
            cancellationToken).ConfigureAwait(false);
        var packages = SyncUpdatesParser.Parse(xml);
        var languages = locale.Split('-');
        return new Fe3PackageList(wuCategoryId, packages, languages);
    }

    /// <summary>
    /// Resolves signed CDN URLs for the given packages.
    /// </summary>
    /// <param name="packages">Packages whose URLs should be resolved.</param>
    /// <param name="locale">Locale reported in the device attributes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Map of base64 SHA-1 digest to download URL.</returns>
    public async Task<IReadOnlyDictionary<string, string>> GetFileUrlsAsync(
        IReadOnlyList<WuPackage> packages,
        string locale = "en-US",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);

        var targets = packages.Where(package => package.HasFileUrl && !string.IsNullOrEmpty(package.UpdateId)).ToList();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var batch in targets.Chunk(FileUrlBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var xml = await PostWithSessionAsync(
                cookie =>
                {
                    var now = DateTimeOffset.UtcNow;
                    return WuSoap.Envelope(
                        "GetExtendedUpdateInfo2",
                        SecuredClientWebServiceUrl,
                        now,
                        now.AddMinutes(10),
                        WuSoap.RetailTicket,
                        WuSoap.FileUrlBody(batch.Select(package => (package.UpdateId, package.RevisionNumber)), locale));
                },
                SecuredClientWebServiceUrl,
                cancellationToken).ConfigureAwait(false);
            CollectFileLocations(WuSoap.ParseDocument(xml), result);
        }

        return result;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private static void CollectFileLocations(XDocument document, Dictionary<string, string> sink)
    {
        foreach (var location in document.Descendants().Where(element => element.Name.LocalName == "FileLocation"))
        {
            var digest = location.Elements().FirstOrDefault(element => element.Name.LocalName == "FileDigest")?.Value;
            var url = location.Elements().FirstOrDefault(element => element.Name.LocalName == "Url")?.Value;
            if (!string.IsNullOrWhiteSpace(digest) && !string.IsNullOrWhiteSpace(url))
            {
                sink[digest] = url;
            }
        }
    }

    /// <summary>
    /// Posts an envelope that carries the session cookie, fetching the cookie when there is none.
    /// </summary>
    /// <param name="envelopeFactory">Builds the envelope around a cookie, so a renewed one is sent.</param>
    /// <param name="url">Endpoint to post to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Session cookies expire. The service answers an expired one with a fault that says nothing about
    /// the cause, so a client that fetches one cookie and keeps it fails every later call - which reads
    /// exactly like a service outage - until the process restarts. On a fault the cookie is dropped and
    /// fetched once more before the same call is attempted again.
    /// </remarks>
    private async Task<string> PostWithSessionAsync(
        Func<string, string> envelopeFactory,
        string url,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopeFactory);

        var cookie = _cookie ?? await GetCookieAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await PostAsync(url, envelopeFactory(cookie), cancellationToken).ConfigureAwait(false);
        }
        catch (Fe3Exception)
        {
            _cookie = null;
            cookie = await GetCookieAsync(cancellationToken).ConfigureAwait(false);
            return await PostAsync(url, envelopeFactory(cookie), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string> PostAsync(string url, string soapEnvelope, CancellationToken cancellationToken)
    {
        using var content = new StringContent(soapEnvelope, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/soap+xml") { CharSet = "utf-8" };

        using var response = await HttpRetry.SendAsync(
            _httpClient,
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/soap+xml"));
                return request;
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new Fe3Exception($"{(int)response.StatusCode} {response.ReasonPhrase} from {url}. {ExtractFault(body)}");
        }

        if (body.Contains("<s:Fault", StringComparison.Ordinal) || body.Contains(":Fault>", StringComparison.Ordinal))
        {
            throw new Fe3Exception($"The delivery service answered with a fault. {ExtractFault(body)}");
        }

        return body;
    }

    private static string ExtractFault(string body)
    {
        var index = body.IndexOf("Fault", StringComparison.Ordinal);
        if (index < 0)
        {
            return body.Length > 400 ? body[..400] + "..." : body;
        }

        var start = Math.Max(0, index - 40);
        var length = Math.Min(body.Length - start, 400);
        return body.Substring(start, length);
    }
}
