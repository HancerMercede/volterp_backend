using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace Volterp.DgiiSimulator.Tests;

public sealed class DgiiSimulatorTests(DgiiSimulatorFixture fixture) : IClassFixture<DgiiSimulatorFixture>
{
    private const string Environment = "testecf";
    private const string IssuerRnc = "101500018";
    private const string Encf = "E320000000001";
    private const string VerdictHeader = "X-DGII-Simulator-Verdict";

    [Fact]
    public async Task SeedEndpoint_ReturnsTheDgiiSemillaModel()
    {
        using var response = await fixture.Client.GetAsync(
            $"/{Environment}/autenticacion/api/autenticacion/semilla");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seed = XDocument.Parse(body);
        Assert.Equal("SemillaModel", seed.Root!.Name.LocalName);
        Assert.False(string.IsNullOrWhiteSpace(seed.Root.Element("valor")?.Value));
        Assert.True(DateTimeOffset.TryParse(
            seed.Root.Element("fecha")?.Value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out _));
    }

    [Fact]
    public async Task ValidateSeed_WithAValidSignature_IssuesAOneHourToken()
    {
        var seed = await GetSeedAsync();
        using var certificate = TestXml.CreateCertificate();

        using var response = await PostSignedSeedAsync(seed, certificate);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        var token = json.RootElement.GetProperty("token").GetString()!;
        var issuedAt = DateTimeOffset.Parse(json.RootElement.GetProperty("expedido").GetString()!, CultureInfo.InvariantCulture);
        var expiresAt = DateTimeOffset.Parse(json.RootElement.GetProperty("expira").GetString()!, CultureInfo.InvariantCulture);
        Assert.Equal(TimeSpan.FromHours(1), expiresAt - issuedAt);
    }

    [Fact]
    public async Task IssuedToken_IsSignedByTheSimulatorCertificate()
    {
        var seed = await GetSeedAsync();
        using var certificate = TestXml.CreateCertificate();
        using var response = await PostSignedSeedAsync(seed, certificate);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = json.RootElement.GetProperty("token").GetString()!;

        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        var signedBytes = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        var signature = Base64UrlDecode(parts[2]);
        using var rsa = fixture.Simulator.Certificate.GetRSAPublicKey()!;

        Assert.True(rsa.VerifyData(
            signedBytes,
            signature,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public async Task ValidateSeed_WithoutSignature_IsRejected()
    {
        var seed = await GetSeedAsync();

        using var response = await PostXmlAsync(
            seed,
            $"response_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.xml");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ValidateSeed_WithAValueThatWasNotIssued_IsRejected()
    {
        var seed = await GetSeedAsync();
        var altered = seed.Replace(
            XDocument.Parse(seed).Root!.Element("valor")!.Value,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            StringComparison.Ordinal);
        using var certificate = TestXml.CreateCertificate();

        using var response = await PostSignedSeedAsync(altered, certificate);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reception_WithAValidToken_AcceptsTheEcfAndReturnsItsAcknowledgement()
    {
        var token = await GetTokenAsync();
        using var certificate = TestXml.CreateCertificate();

        using var response = await PostEcfAsync(token, certificate);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.True(Guid.TryParse(json.RootElement.GetProperty("trackId").GetString(), out _));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("error").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("mensaje").GetString()));
    }

    [Fact]
    public async Task Status_WithTheDefaultVerdict_IsAccepted()
    {
        var token = await GetTokenAsync();
        var trackId = await ReceiveEcfAsync(token);

        using var response = await GetStatusAsync(token, trackId);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.RootElement.GetProperty("codigo").GetInt32());
        Assert.Equal("Aceptado", json.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Status_CanBeRejectedForOneDocument()
    {
        var token = await GetTokenAsync();
        var trackId = await ReceiveEcfAsync(token, "rejected");

        using var response = await GetStatusAsync(token, trackId);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(2, json.RootElement.GetProperty("codigo").GetInt32());
        Assert.Equal("Rechazado", json.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Status_CanRemainInProcessForOneDocument()
    {
        var token = await GetTokenAsync();
        var trackId = await ReceiveEcfAsync(token, "in-process");

        using var response = await GetStatusAsync(token, trackId);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(3, json.RootElement.GetProperty("codigo").GetInt32());
        Assert.Equal("En Proceso", json.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Reception_WithAnInvalidToken_IsRejectedAndStoresNothing()
    {
        using var certificate = TestXml.CreateCertificate();

        using var response = await PostEcfAsync("not-a-token", certificate);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var token = await GetTokenAsync();
        using var lookupRequest = WithBearer(
            token,
            $"/{Environment}/consultatrackids/api/trackids/consulta?rncemisor={IssuerRnc}&encf={Encf}");
        using var lookup = await fixture.Client.SendAsync(lookupRequest);
        using var json = JsonDocument.Parse(await lookup.Content.ReadAsStringAsync());
        Assert.Equal(string.Empty, json.RootElement.GetProperty("trackId").GetString());
    }

    [Fact]
    public async Task TrackIds_FindsTheTrackIdByIssuerAndEncf()
    {
        var token = await GetTokenAsync();
        var trackId = await ReceiveEcfAsync(token);

        using var request = WithBearer(
            token,
            $"/{Environment}/consultatrackids/api/trackids/consulta?rncemisor={IssuerRnc}&encf={Encf}");
        using var response = await fixture.Client.SendAsync(request);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(trackId, json.RootElement.GetProperty("trackId").GetString());
        Assert.Equal("Aceptado", json.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task ConsultaEstado_ReturnsTheStoredVerdict()
    {
        var token = await GetTokenAsync();
        await ReceiveEcfAsync(token);

        using var request = WithBearer(
            token,
            $"/{Environment}/consultaestado/api/consultas/estado?rncemisor={IssuerRnc}&ncfelectronico={Encf}");
        using var response = await fixture.Client.SendAsync(request);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.RootElement.GetProperty("codigo").GetInt32());
        Assert.Equal(IssuerRnc, json.RootElement.GetProperty("rncEmisor").GetString());
        Assert.Equal(Encf, json.RootElement.GetProperty("ncfElectronico").GetString());
    }

    private async Task<string> GetSeedAsync()
    {
        using var response = await fixture.Client.GetAsync(
            $"/{Environment}/autenticacion/api/autenticacion/semilla");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> GetTokenAsync()
    {
        var seed = await GetSeedAsync();
        using var certificate = TestXml.CreateCertificate();
        using var response = await PostSignedSeedAsync(seed, certificate);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }

    private async Task<HttpResponseMessage> PostSignedSeedAsync(string seed, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate)
    {
        var signed = TestXml.Sign(seed, certificate);
        return await PostXmlAsync(signed, $"response_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.xml");
    }

    private async Task<HttpResponseMessage> PostXmlAsync(string xml, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(xml));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
        form.Add(content, "xml", fileName);

        return await fixture.Client.PostAsync(
            $"/{Environment}/autenticacion/api/autenticacion/validarsemilla",
            form);
    }

    private async Task<string> ReceiveEcfAsync(string token, string? verdict = null)
    {
        using var certificate = TestXml.CreateCertificate();
        using var response = await PostEcfAsync(token, certificate, verdict);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("trackId").GetString()!;
    }

    private async Task<HttpResponseMessage> PostEcfAsync(
        string token,
        System.Security.Cryptography.X509Certificates.X509Certificate2 certificate,
        string? verdict = null)
    {
        var xml = TestXml.Sign(EcfXml(), certificate);
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(xml));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
        form.Add(content, "xml", $"{IssuerRnc}{Encf}.xml");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{Environment}/recepcion/api/facturaselectronicas")
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (verdict is not null)
            request.Headers.Add(VerdictHeader, verdict);

        return await fixture.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetStatusAsync(string token, string trackId)
    {
        var request = WithBearer(
            token,
            $"/{Environment}/consultaresultado/api/consultas/estado?trackid={trackId}");
        return await fixture.Client.SendAsync(request);
    }

    private static HttpRequestMessage WithBearer(string token, string relativeUrl)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string EcfXml() =>
        "<ECF><Encabezado><eNCF>E320000000001</eNCF><Emisor><RNCEmisor>101500018</RNCEmisor></Emisor>" +
        "<Comprador><RNCComprador>101500019</RNCComprador></Comprador><FechaEmision>2026-11-15</FechaEmision>" +
        "</Encabezado>" +
        "<Totales><MontoTotal>100.00</MontoTotal><TotalITBIS>18.00</TotalITBIS></Totales></ECF>";

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
