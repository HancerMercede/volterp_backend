using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Volterp.ElectronicInvoicing.Dgii;

// Every DGII service lives under its own path segment of the same host, and the
// environment is the segment right after it: testecf, certecf or ecf.
public sealed class DgiiClient(HttpClient httpClient, DgiiOptions options) : IDgiiClient
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly DgiiOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async Task<string> GetSeedAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(DgiiEndpoints.Seed));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new DgiiException(
                $"The DGII refused to hand out a seed ({response.StatusCode}).",
                body,
                statusCode: response.StatusCode);

        return body;
    }

    public async Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signedSeedXml);

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(DgiiEndpoints.ValidateSeed));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = BuildXmlForm(signedSeedXml, ResponseFileName());

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new DgiiException(
                $"The DGII rejected the signed seed ({response.StatusCode}).",
                body,
                statusCode: response.StatusCode);

        var parsed = Deserialize<DgiiTokenResponse>(body, "token");
        if (string.IsNullOrWhiteSpace(parsed.Token))
            throw new DgiiException("The DGII answered the seed without a token.", body);

        return new DgiiToken(
            parsed.Token,
            ParseDate(parsed.Expira),
            ParseDate(parsed.Expedido));
    }

    public async Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signedEcfXml);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(DgiiEndpoints.ReceiveElectronicInvoice));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Authorization", $"bearer {token}");
        request.Content = BuildXmlForm(signedEcfXml, EcfFileName(signedEcfXml));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new DgiiException(
                $"The DGII rejected the e-CF file ({response.StatusCode}).",
                body,
                statusCode: response.StatusCode);

        var parsed = Deserialize<DgiiReceiptResponse>(body, "acknowledgement");

        return new DgiiReceipt(parsed.TrackId, parsed.Error, parsed.Message) { RawBody = body };
    }

    public async Task<DgiiStatus> GetStatusAsync(string trackId, string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var url = BuildUrl(DgiiEndpoints.StatusByTrackId)
                  + "?trackid=" + Uri.EscapeDataString(trackId);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Authorization", $"bearer {token}");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new DgiiException(
                $"The DGII failed to answer the status of {trackId} ({response.StatusCode}).",
                body,
                statusCode: response.StatusCode);

        var parsed = Deserialize<DgiiStatusResponse>(body, "status");

        var messages = parsed.Mensajes?
            .Select(m => new DgiiMessage(m.Value, m.Code))
            .ToList() ?? [];

        return new DgiiStatus(
            parsed.TrackId,
            (DgiiVerdict)parsed.Codigo,
            parsed.Estado,
            parsed.Rnc,
            parsed.Encf,
            parsed.SecuenciaUtilizada,
            ParseDate(parsed.FechaRecepcion),
            messages)
        { RawBody = body };
    }

    public async Task<DgiiTrackLookup> GetTrackIdAsync(string issuerTaxId, string encf, string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerTaxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(encf);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var url = BuildUrl(DgiiEndpoints.TrackIdByEncf)
                  + "?rncemisor=" + Uri.EscapeDataString(issuerTaxId)
                  + "&encf=" + Uri.EscapeDataString(encf);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Authorization", $"bearer {token}");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new DgiiException(
                $"The DGII failed to answer for the e-CF {encf} ({response.StatusCode}).",
                body,
                statusCode: response.StatusCode);

        var parsed = Deserialize<DgiiTrackLookupResponse>(body, "track id");

        return new DgiiTrackLookup(parsed.TrackId, parsed.Estado, ParseDate(parsed.FechaRecepcion));
    }

    public async Task<DgiiRangeAnnulmentAnswer> AnnulRangesAsync(string signedAnecfXml, string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signedAnecfXml);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(DgiiEndpoints.AnnulRanges));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Authorization", $"bearer {token}");
        request.Content = BuildXmlForm(signedAnecfXml, ResponseFileName());

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new DgiiException(
                $"The DGII rejected the annulment of the ranges ({response.StatusCode}).",
                body,
                statusCode: response.StatusCode);

        var parsed = Deserialize<DgiiRangeAnnulmentResponse>(body, "annulment");

        return new DgiiRangeAnnulmentAnswer(
            parsed.Rnc,
            parsed.Codigo,
            parsed.Nombre,
            parsed.Mensajes ?? [])
        { RawBody = body };
    }

    private string BuildUrl(string relativePath)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/{_options.Environment.ToPathSegment()}/{relativePath}";
    }

    // The DGII takes the XML as a multipart file named xml, typed as text/xml.
    // The name of the file is part of the contract: the seed is named
    // response_{timestamp}.xml and a document RNC{e-NCF}.xml.
    private static MultipartFormDataContent BuildXmlForm(string xml, string fileName)
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(xml));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
        form.Add(content, "xml", fileName);
        return form;
    }

    // The name of a file that is not named after an e-NCF: the seed and the
    // annulment of the ranges take the shape of the response.
    private static string ResponseFileName()
        => $"response_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.xml";

    // The name the DGII expects for a document: the issuer RNC followed by the
    // e-NCF, as the "Descripción Técnica Servicios DGII" says.
    private static string EcfFileName(string xml)
    {
        var document = XDocument.Parse(xml);

        var issuer = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "RNCEmisor")?.Value;
        var encf = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "eNCF")?.Value
                   ?? document.Descendants().FirstOrDefault(e => e.Name.LocalName == "NcfElectronico")?.Value;

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(encf))
            throw new DgiiException("The document has no issuer RNC and e-NCF to name the file with.");

        return $"{issuer}{encf}.xml";
    }

    private static T Deserialize<T>(string body, string what)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<T>(body, Json);
            if (parsed is null)
                throw new DgiiException($"The DGII answered the {what} with an empty body.", body);

            return parsed;
        }
        catch (JsonException exception)
        {
            throw new DgiiException($"The DGII answered the {what} with a body that is not the expected JSON.", body, exception);
        }
    }

    // A date we cannot read must not cost us the whole answer.
    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }
}
