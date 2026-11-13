using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Volterp.ElectronicInvoicing.Dgii;

/// <summary>
/// The DGII client, implemented against the web services the DGII publishes.
/// </summary>
/// <remarks>
/// Every DGII service lives under its own path segment of the same host, and the
/// environment is the segment right after it: testecf, certecf or ecf.
/// </remarks>
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
                $"The DGII refused to hand out a seed ({response.StatusCode}).", body);

        return body;
    }

    public async Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signedSeedXml);

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(DgiiEndpoints.ValidateSeed));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = BuildXmlForm(signedSeedXml);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new DgiiException($"The DGII rejected the signed seed ({response.StatusCode}).", body);

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
        request.Content = BuildXmlForm(signedEcfXml);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new DgiiException($"The DGII rejected the e-CF file ({response.StatusCode}).", body);

        var parsed = Deserialize<DgiiReceiptResponse>(body, "acknowledgement");

        return new DgiiReceipt(parsed.TrackId, parsed.Error, parsed.Message);
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
            throw new DgiiException($"The DGII failed to answer the status of {trackId} ({response.StatusCode}).", body);

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
            messages);
    }

    private string BuildUrl(string relativePath)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/{_options.Environment.ToPathSegment()}/{relativePath}";
    }

    /// <summary>
    /// The DGII takes the XML as a multipart field named xml, typed as text/xml.
    /// </summary>
    private static MultipartFormDataContent BuildXmlForm(string xml)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(xml, Encoding.UTF8, "text/xml"), "xml");
        return form;
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

    /// <summary>
    /// The DGII dates are not guaranteed to be parseable, and a date we cannot
    /// read must not cost us the whole answer.
    /// </summary>
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
