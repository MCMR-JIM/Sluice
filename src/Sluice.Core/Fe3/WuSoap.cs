using System.Xml.Linq;

namespace Sluice.Fe3;

/// <summary>Builds the SOAP envelopes the delivery service expects.</summary>
public static class WuSoap
{
    /// <summary>SOAP action namespace prefix used by the client web service.</summary>
    public const string ActionNamespace = "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService/";

    /// <summary>Ticket token used for cookie requests.</summary>
    public const string UserTicket = "<User />";

    /// <summary>Ticket token a retail Windows Update client presents for payload requests.</summary>
    public const string RetailTicket = "Retail";

    /// <summary>Builds a WS-Addressing/WS-Security envelope around a body fragment.</summary>
    /// <param name="action">Action name, for example <c>GetCookie</c>.</param>
    /// <param name="toUrl">Endpoint URL the request is addressed to.</param>
    /// <param name="created">WS-Security <c>Created</c> timestamp.</param>
    /// <param name="expires">WS-Security <c>Expires</c> timestamp.</param>
    /// <param name="ticketToken">Ticket token body, typically <see cref="RetailTicket"/>.</param>
    /// <param name="body">XML fragment placed inside the action element.</param>
    public static string Envelope(
        string action,
        string toUrl,
        DateTimeOffset created,
        DateTimeOffset expires,
        string ticketToken,
        string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(toUrl);

        return $"""
            <s:Envelope xmlns:a="http://www.w3.org/2005/08/addressing" xmlns:s="http://www.w3.org/2003/05/soap-envelope">
              <s:Header>
                <a:Action s:mustUnderstand="1">{ActionNamespace}{action}</a:Action>
                <a:MessageID>urn:uuid:{Guid.NewGuid()}</a:MessageID>
                <a:To s:mustUnderstand="1">{toUrl}</a:To>
                <o:Security s:mustUnderstand="1" xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
                  <Timestamp xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
                    <Created>{Format(created)}</Created>
                    <Expires>{Format(expires)}</Expires>
                  </Timestamp>
                  <wuws:WindowsUpdateTicketsToken wsu:id="ClientMSA"
                      xmlns:wsu="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"
                      xmlns:wuws="http://schemas.microsoft.com/msus/2014/10/WindowsUpdateAuthorization">
                    <TicketType Name="MSA" Version="1.0" Policy="MBI_SSL">{ticketToken}</TicketType>
                  </wuws:WindowsUpdateTicketsToken>
                </o:Security>
              </s:Header>
              <s:Body>
                <{action} xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
            {body}
                </{action}>
              </s:Body>
            </s:Envelope>
            """;
    }

    /// <summary>Builds the <c>GetCookie</c> body.</summary>
    /// <param name="now">Current time.</param>
    public static string GetCookieBody(DateTimeOffset now) => $"""
          <oldCookie></oldCookie>
          <lastChange>{Format(now.AddDays(-730))}</lastChange>
          <currentTime>{Format(now)}</currentTime>
          <protocolVersion>1.40</protocolVersion>
        """;

    /// <summary>Builds the <c>SyncUpdates</c> body for one Windows Update category.</summary>
    /// <param name="cookie">Cookie returned by <see cref="GetCookieBody"/>.</param>
    /// <param name="wuCategoryId">Category to expand.</param>
    /// <param name="locale">Locale reported as client preferred language.</param>
    public static string SyncUpdatesBody(string cookie, string wuCategoryId, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cookie);
        ArgumentException.ThrowIfNullOrWhiteSpace(wuCategoryId);

        var language = string.IsNullOrWhiteSpace(locale) ? "en-US" : locale;
        var languageRoot = language.Split('-')[0];

        return $"""
          <cookie>
            <Expiration>2045-03-11T02:02:48Z</Expiration>
            <EncryptedData>{cookie}</EncryptedData>
          </cookie>
          <parameters>
            <ExpressQuery>false</ExpressQuery>
            {IntList("InstalledNonLeafUpdateIDs", WuDeviceProfile.InstalledNonLeafUpdateIds)}
            {IntList("OtherCachedUpdateIDs", WuDeviceProfile.OtherCachedUpdateIds)}
            <SkipSoftwareSync>false</SkipSoftwareSync>
            <NeedTwoGroupOutOfScopeUpdates>true</NeedTwoGroupOutOfScopeUpdates>
            <FilterAppCategoryIds>
              <CategoryIdentifier>
                <Id>{wuCategoryId}</Id>
              </CategoryIdentifier>
            </FilterAppCategoryIds>
            <TreatAppCategoryIdsAsInstalled>true</TreatAppCategoryIdsAsInstalled>
            <AlsoPerformRegularSync>false</AlsoPerformRegularSync>
            <ComputerSpec/>
            <ExtendedUpdateInfoParameters>
              <XmlUpdateFragmentTypes>
                <XmlUpdateFragmentType>Extended</XmlUpdateFragmentType>
              </XmlUpdateFragmentTypes>
              <Locales>
                <string>{language}</string>
                <string>{languageRoot}</string>
              </Locales>
            </ExtendedUpdateInfoParameters>
            <ClientPreferredLanguages>
              <string>{language}</string>
              <string>{languageRoot}</string>
            </ClientPreferredLanguages>
            <ProductsParameters>
              <SyncCurrentVersionOnly>false</SyncCurrentVersionOnly>
              <DeviceAttributes>{WuDeviceProfile.DeviceAttributes(language)}</DeviceAttributes>
              <CallerAttributes>Interactive=1;IsSeeker=0;</CallerAttributes>
              <Products/>
            </ProductsParameters>
          </parameters>
        """;
    }

    /// <summary>Builds the <c>GetExtendedUpdateInfo2</c> body for a batch of updates.</summary>
    /// <param name="updates">Updates to resolve URLs for.</param>
    /// <param name="locale">Locale reported in the device attributes.</param>
    public static string FileUrlBody(IEnumerable<(string UpdateId, int RevisionNumber)> updates, string locale)
    {
        ArgumentNullException.ThrowIfNull(updates);

        var identities = string.Concat(updates.Select(update =>
            $"<UpdateIdentity><UpdateID>{update.UpdateId}</UpdateID><RevisionNumber>{update.RevisionNumber}</RevisionNumber></UpdateIdentity>"));

        return $"""
          <updateIDs>{identities}</updateIDs>
          <infoTypes>
            <XmlUpdateFragmentType>FileUrl</XmlUpdateFragmentType>
            <XmlUpdateFragmentType>FileDecryption</XmlUpdateFragmentType>
          </infoTypes>
          <deviceAttributes>{WuDeviceProfile.DeviceAttributes(locale)}</deviceAttributes>
        """;
    }

    private static string IntList(string elementName, IEnumerable<int> values) =>
        $"<{elementName}>{string.Concat(values.Select(value => $"<int>{value}</int>"))}</{elementName}>";

    /// <summary>Formats a timestamp as the WU protocol expects it.</summary>
    /// <param name="value">Timestamp to format.</param>
    public static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Parses a response document, tolerating the HTML escaping some endpoints use.</summary>
    /// <param name="xml">Raw response body.</param>
    public static XDocument ParseDocument(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        return XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
    }
}
