using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Volterp.DgiiSimulator.Tests;

/// <summary>
/// The knobs of the simulator: the verdicts it can carry, the failures it can
/// inject and the delays it can hold a caller with.
/// </summary>
public sealed class DgiiSimulatorControlTests
{
    private const string IssuerRnc = SimulatorFlow.IssuerRnc;
    private const string Encf = SimulatorFlow.Encf;
    private const string ErrorHeader = "X-DGII-Simulator-Error";

    [Fact]
    public async Task Status_CanBeAcceptedConditionally()
    {
        await using var simulator = await SimulatorFlow.StartAsync();
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        var token = await SimulatorFlow.GetTokenAsync(client);
        var trackId = await SimulatorFlow.ReceiveAsync(client, token, verdict: "accepted-conditionally");

        using var response = await SimulatorFlow.GetAsync(
            client,
            token,
            $"consultaresultado/api/consultas/estado?trackid={trackId}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, json.RootElement.GetProperty("codigo").GetInt32());
        Assert.Equal("Aceptado Condicional", json.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Status_OfAnUnknownTrackId_IsNotFound()
    {
        await using var simulator = await SimulatorFlow.StartAsync();
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        var token = await SimulatorFlow.GetTokenAsync(client);
        var unknown = Guid.NewGuid().ToString();

        using var response = await SimulatorFlow.GetAsync(
            client,
            token,
            $"consultaresultado/api/consultas/estado?trackid={unknown}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, json.RootElement.GetProperty("codigo").GetInt32());
        Assert.Equal("No encontrado", json.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Consultations_WithoutAToken_AreUnauthorized()
    {
        await using var simulator = await SimulatorFlow.StartAsync();
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        using var status = await client.GetAsync(
            $"/{SimulatorFlow.Environment}/consultaresultado/api/consultas/estado?trackid=x");
        using var trackIds = await client.GetAsync(
            $"/{SimulatorFlow.Environment}/consultatrackids/api/trackids/consulta?rncemisor={IssuerRnc}&encf={Encf}");
        using var state = await client.GetAsync(
            $"/{SimulatorFlow.Environment}/consultaestado/api/consultas/estado?rncemisor={IssuerRnc}&ncfelectronico={Encf}");

        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, trackIds.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, state.StatusCode);
    }

    [Fact]
    public async Task Reception_WithoutTheXmlFile_IsRejected()
    {
        await using var simulator = await SimulatorFlow.StartAsync();
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        var token = await SimulatorFlow.GetTokenAsync(client);

        using var form = new MultipartFormDataContent();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{SimulatorFlow.Environment}/recepcion/api/facturaselectronicas")
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reception_WithAnUnsignedDocument_IsRejectedAndStoresNothing()
    {
        await using var simulator = await SimulatorFlow.StartAsync();
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        var token = await SimulatorFlow.GetTokenAsync(client);

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(SimulatorFlow.EcfXml()));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
        form.Add(content, "xml", $"{IssuerRnc}{Encf}.xml");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{SimulatorFlow.Environment}/recepcion/api/facturaselectronicas")
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var lookup = await SimulatorFlow.GetAsync(
            client,
            token,
            $"consultatrackids/api/trackids/consulta?rncemisor={IssuerRnc}&encf={Encf}");
        using var json = JsonDocument.Parse(await lookup.Content.ReadAsStringAsync());
        Assert.Equal(string.Empty, json.RootElement.GetProperty("trackId").GetString());
    }

    [Fact]
    public async Task Reception_WithADocumentThatDoesNotSayWhoEmitsIt_IsRejected()
    {
        await using var simulator = await SimulatorFlow.StartAsync();
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        var token = await SimulatorFlow.GetTokenAsync(client);

        using var certificate = TestXml.CreateCertificate();
        var signed = TestXml.Sign(
            "<ECF><Encabezado><Comprador><RNCComprador>101500019</RNCComprador></Comprador>" +
            "</Encabezado></ECF>",
            certificate);

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(signed));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
        form.Add(content, "xml", $"{IssuerRnc}{Encf}.xml");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{SimulatorFlow.Environment}/recepcion/api/facturaselectronicas")
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reception_UsesTheConfiguredDefaultVerdict()
    {
        await using var simulator = await SimulatorFlow.StartAsync(new DgiiSimulatorOptions
        {
            BaseUrl = "http://127.0.0.1:0",
            Environment = SimulatorFlow.Environment,
            DefaultVerdict = SimulatorVerdict.Rejected
        });
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        var token = await SimulatorFlow.GetTokenAsync(client);
        var trackId = await SimulatorFlow.ReceiveAsync(client, token);

        using var response = await SimulatorFlow.GetAsync(
            client,
            token,
            $"consultaresultado/api/consultas/estado?trackid={trackId}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(2, json.RootElement.GetProperty("codigo").GetInt32());
        Assert.Equal("Rechazado", json.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Reception_HonoursTheConfiguredDelay()
    {
        const int delay = 400;

        await using var simulator = await SimulatorFlow.StartAsync(new DgiiSimulatorOptions
        {
            BaseUrl = "http://127.0.0.1:0",
            Environment = SimulatorFlow.Environment,
            ReceptionDelayMilliseconds = delay
        });
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        var token = await SimulatorFlow.GetTokenAsync(client);

        var watch = Stopwatch.StartNew();
        var trackId = await SimulatorFlow.ReceiveAsync(client, token);
        watch.Stop();

        Assert.NotEmpty(trackId);
        Assert.True(
            watch.ElapsedMilliseconds >= delay - 50,
            $"the reception took {watch.ElapsedMilliseconds}ms and the configured delay was {delay}ms");
    }

    [Fact]
    public async Task Reception_FailsWithTheInjectedError()
    {
        await using var simulator = await SimulatorFlow.StartAsync();
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        var token = await SimulatorFlow.GetTokenAsync(client);

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(SimulatorFlow.SignedEcfXml()));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
        form.Add(content, "xml", $"{IssuerRnc}{Encf}.xml");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{SimulatorFlow.Environment}/recepcion/api/facturaselectronicas")
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(ErrorHeader, "503");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public void TheSimulatorBindsToLoopbackByDefault()
    {
        var host = new Uri(new DgiiSimulatorOptions().BaseUrl).Host;

        Assert.True(
            host is "127.0.0.1" or "localhost" or "::1",
            $"the simulator listens on {host} and it has to be the loopback unless it is configured otherwise");
    }

    [Fact]
    public void TheSimulatorCarriesNoKeyMaterial()
    {
        var projectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var forbidden = new[] { ".p12", ".pfx", ".pem", ".key", ".jks", ".keystore" };

        var found = Directory
            .EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories)
            .Where(path => forbidden.Contains(Path.GetExtension(path).ToLowerInvariant()))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        Assert.Empty(found);
    }
}
