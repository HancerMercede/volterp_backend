using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Volterp.DgiiSimulator.Tests;

/// <summary>
/// The circuit the simulator is exercised with: seed, signed seed, token,
/// reception and consultation.
/// </summary>
internal static class SimulatorFlow
{
    public const string Environment = "testecf";
    public const string IssuerRnc = "101500018";
    public const string Encf = "E320000000001";
    public const string VerdictHeader = "X-DGII-Simulator-Verdict";

    public static Task<DgiiSimulatorApplication> StartAsync(DgiiSimulatorOptions? options = null) =>
        DgiiSimulatorApplication.StartAsync(options ?? new DgiiSimulatorOptions
        {
            BaseUrl = "http://127.0.0.1:0",
            Environment = Environment
        });

    public static async Task<string> GetTokenAsync(HttpClient client)
    {
        using var seedResponse = await client.GetAsync($"/{Environment}/autenticacion/api/autenticacion/semilla");
        seedResponse.EnsureSuccessStatusCode();
        var seed = await seedResponse.Content.ReadAsStringAsync();

        using var certificate = TestXml.CreateCertificate();
        var signed = TestXml.Sign(seed, certificate);

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(signed));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
        form.Add(content, "xml", $"response_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.xml");

        using var response = await client.PostAsync(
            $"/{Environment}/autenticacion/api/autenticacion/validarsemilla",
            form);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }

    public static async Task<string> ReceiveAsync(
        HttpClient client,
        string token,
        string? signedXml = null,
        string? verdict = null,
        string? fileName = null)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(signedXml ?? SignedEcfXml()));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
        form.Add(content, "xml", fileName ?? $"{IssuerRnc}{Encf}.xml");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{Environment}/recepcion/api/facturaselectronicas")
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (verdict is not null)
            request.Headers.Add(VerdictHeader, verdict);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("trackId").GetString()!;
    }

    public static Task<HttpResponseMessage> GetAsync(HttpClient client, string token, string relativePath)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/{Environment}/{relativePath}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    public static string EcfXml(string variant = "") =>
        "<ECF><Encabezado><eNCF>E320000000001</eNCF><Emisor><RNCEmisor>101500018</RNCEmisor></Emisor>"
        + "<Comprador><RNCComprador>101500019</RNCComprador></Comprador><FechaEmision>2026-11-15</FechaEmision>"
        + variant
        + "</Encabezado>"
        + "<Totales><MontoTotal>100.00</MontoTotal><TotalITBIS>18.00</TotalITBIS></Totales></ECF>";

    public static string SignedEcfXml(string variant = "")
    {
        using var certificate = TestXml.CreateCertificate();
        return TestXml.Sign(EcfXml(variant), certificate);
    }

    public static string? ExtractSignatureValue(string signedXml)
    {
        var document = XDocument.Parse(signedXml);
        return document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "SignatureValue")
            ?.Value
            .Trim();
    }
}
